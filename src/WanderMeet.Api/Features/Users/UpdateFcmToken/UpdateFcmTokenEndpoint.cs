using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Users.UpdateFcmToken;

/// <summary>Updates the authenticated user's FCM device registration token.</summary>
internal sealed class UpdateFcmTokenEndpoint(WanderMeetDbContext dbContext, TimeProvider timeProvider)
    : Endpoint<UpdateFcmTokenRequest>
{
    private readonly UsersFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Patch("users/me/fcm-token");
        Description(b => b
            .WithName(nameof(UpdateFcmTokenEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        DontThrowIfValidationFails();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "Update FCM device token";
            s.Description = "Stores or replaces the Firebase Cloud Messaging token for the authenticated user's device.";
            s.Responses[StatusCodes.Status204NoContent] = "Token updated successfully";
            s.Responses[StatusCodes.Status400BadRequest] = "Token is missing, empty, whitespace-only, or exceeds 512 characters";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "No user profile found for this identity";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(UpdateFcmTokenRequest req, CancellationToken ct)
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

        var now = timeProvider.GetUtcNow();

        var rows = await dbContext.Users
            .Where(u => u.AzureAdB2CId == sub && u.DeletedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.FcmToken, req.Token)
                .SetProperty(u => u.LastActiveAt, now), ct);

        if (rows == 0)
        {
            AddError(ErrorCodes.User.NotRegistered, "No user profile found for this identity.");
            await Send.ErrorsAsync(404, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
