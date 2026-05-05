using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Infrastructure.EntityFramework;

namespace WanderMeet.Api.Features.Users.ToggleOpenToday;

/// <summary>Toggles the authenticated user's open-today status.</summary>
internal sealed class ToggleOpenTodayEndpoint(WanderMeetDbContext dbContext, TimeProvider timeProvider)
    : Endpoint<ToggleOpenTodayRequest>
{
    private readonly UsersFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Patch("users/me/open-today");
        Description(b => b
            .WithName(nameof(ToggleOpenTodayEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "Toggle open-today status";
            s.Description = "Updates the authenticated user's IsOpenToday flag without loading the full entity.";
            s.Responses[StatusCodes.Status204NoContent] = "Status updated successfully";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "No user profile found for this identity";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(ToggleOpenTodayRequest req, CancellationToken ct)
    {
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
                .SetProperty(u => u.IsOpenToday, req.IsOpen)
                .SetProperty(u => u.LastActiveAt, now), ct);

        if (rows == 0)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
