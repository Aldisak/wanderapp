using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Features.Users.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;

namespace WanderMeet.Api.Features.Users.GetById;

/// <summary>Returns a public profile for the user with the given id.</summary>
internal sealed class GetByIdEndpoint(WanderMeetDbContext dbContext)
    : Endpoint<GetByIdRequest, PublicUserDto>
{
    private readonly UsersFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Get("users/{id:guid}");
        Description(b => b
            .WithName(nameof(GetByIdEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "Get a user's public profile";
            s.Description = "Returns the public-facing profile fields for any non-deleted user.";
            s.Responses[StatusCodes.Status200OK] = "Public profile";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "User not found or soft-deleted";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(GetByIdRequest req, CancellationToken ct)
    {
        var dto = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == req.Id && u.DeletedAt == null)
            .Select(u => new PublicUserDto(
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
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(dto, ct);
    }
}
