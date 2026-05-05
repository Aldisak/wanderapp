using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Features.Users.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Users.GetMe;

/// <summary>Returns the authenticated user's own profile.</summary>
internal sealed class GetMeEndpoint(WanderMeetDbContext dbContext)
    : EndpointWithoutRequest<UserDto>
{
    private readonly UsersFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Get("users/me");
        Description(b => b
            .WithName(nameof(GetMeEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "Get own profile";
            s.Description = "Returns the authenticated user's profile DTO.";
            s.Responses[StatusCodes.Status200OK] = "User profile";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "No user profile found for this identity";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(CancellationToken ct)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sub))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var dto = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.AzureAdB2CId == sub && u.DeletedAt == null)
            .Select(u => new UserDto(
                u.Id,
                u.FirstName,
                u.Bio,
                u.IsIdVerified,
                u.IsOpenToday,
                u.IsOpenToRomance,
                u.LastActiveAt,
                u.TrustScore,
                u.MeetupCount,
                u.CitiesCount,
                u.YearsNomading,
                u.CityId,
                u.CreatedAt,
                u.HangoutTags.Select(ht => ht.HangoutTagId).ToList()))
            .FirstOrDefaultAsync(ct);

        if (dto is null)
        {
            AddError(ErrorCodes.User.NotRegistered, "No user profile found for this identity.");
            await Send.ErrorsAsync(404, ct);
            return;
        }

        await Send.OkAsync(dto, ct);
    }
}
