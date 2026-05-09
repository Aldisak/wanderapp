using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Reports.SubmitReport;

/// <summary>Submits a user-to-user report (intake only). Rate-limited to 5/day per reporter.</summary>
internal sealed class SubmitReportEndpoint(
    WanderMeetDbContext dbContext,
    TimeProvider timeProvider)
    : Endpoint<SubmitReportRequest>
{
    private readonly ReportsFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Post("reports");
        Description(b => b
            .WithName(nameof(SubmitReportEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.Reports));
        DontCatchExceptions();
        DontThrowIfValidationFails();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "Submit a report about another user";
            s.Description = "Creates a new Report row. Rate-limited to 5 per day per authenticated user.";
            s.Responses[StatusCodes.Status204NoContent] = "Report submitted";
            s.Responses[StatusCodes.Status400BadRequest] = "Validation error or self-report attempt";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "Caller has no user profile (User.NotRegistered) or reported user not found (Report.UserNotFound)";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded (5 reports/day per user)";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(SubmitReportRequest req, CancellationToken ct)
    {
        if (ValidationFailed)
        {
            var failures = ValidationFailures.ToList();
            ValidationFailures.Clear();
            foreach (var f in failures)
                AddError(f.ErrorCode, f.ErrorMessage);
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sub))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        // Load tracked caller (to update LastActiveAt)
        var caller = await dbContext.Users
            .FirstOrDefaultAsync(u => u.AzureAdB2CId == sub && u.DeletedAt == null, ct);

        if (caller is null)
        {
            AddError(ErrorCodes.User.NotRegistered, "No user profile found for this identity.");
            await Send.ErrorsAsync(404, ct);
            return;
        }

        // Guard: self-report (cheap, no DB lookup needed)
        if (req.ReportedUserId == caller.Id)
        {
            AddError(ErrorCodes.Report.SelfReportForbidden, "You cannot report yourself.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        // Check target user exists and is not soft-deleted
        var targetExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == req.ReportedUserId && u.DeletedAt == null, ct);

        if (!targetExists)
        {
            AddError(ErrorCodes.Report.UserNotFound, "The specified user was not found.");
            await Send.ErrorsAsync(404, ct);
            return;
        }

        var now = timeProvider.GetUtcNow();
        var report = new Report
        {
            Id = Guid.NewGuid(),
            ReporterId = caller.Id,
            ReportedId = req.ReportedUserId,
            Reason = req.Reason.Trim(),
            ReviewedAt = null,
            CreatedAt = now,
        };

        dbContext.Reports.Add(report);
        caller.LastActiveAt = now;
        await dbContext.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}
