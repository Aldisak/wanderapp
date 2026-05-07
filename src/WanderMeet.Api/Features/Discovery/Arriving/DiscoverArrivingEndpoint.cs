using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Features.Users.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Discovery.Arriving;

/// <summary>Returns users who will arrive in the requested city within the next 30 days.</summary>
internal sealed class DiscoverArrivingEndpoint(WanderMeetDbContext dbContext, TimeProvider timeProvider)
    : Endpoint<DiscoverArrivingRequest, DiscoverArrivingResponse>
{
    private readonly DiscoveryFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Get("discover/arriving");
        Description(b => b
            .WithName(nameof(DiscoverArrivingEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.Discovery));
        DontCatchExceptions();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "List users arriving soon";
            s.Description = "Returns users who will arrive in the requested city within the next 30 days.";
            s.Responses[StatusCodes.Status200OK] = "List of arriving users";
            s.Responses[StatusCodes.Status400BadRequest] = "Validation error";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "Caller has no user profile or city not found";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(DiscoverArrivingRequest req, CancellationToken ct)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sub))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var callerId = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.AzureAdB2CId == sub && u.DeletedAt == null)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);

        if (callerId is null)
        {
            AddError(ErrorCodes.User.NotRegistered, "No user profile found for this identity.");
            await Send.ErrorsAsync(404, ct);
            return;
        }

        var cityExists = await dbContext.Cities
            .AsNoTracking()
            .AnyAsync(c => c.Id == req.CityId && c.DeletedAt == null, ct);

        if (!cityExists)
        {
            AddError(ErrorCodes.Discovery.CityNotFound, "City not found.");
            await Send.ErrorsAsync(404, ct);
            return;
        }

        var now = timeProvider.GetUtcNow();
        var until = now.AddDays(30);

        var rows = await dbContext.UserCities.AsNoTracking()
            .Where(uc => uc.CityId == req.CityId)
            .Where(uc => uc.ArrivedAt > now && uc.ArrivedAt <= until)
            .Where(uc => uc.DepartedAt == null)
            .Where(uc => uc.UserId != callerId.Value)
            .Where(uc => uc.User!.DeletedAt == null)
            .OrderBy(uc => uc.ArrivedAt)
            .Select(uc => new ArrivingUserDto(
                new PublicUserDto(
                    uc.User!.Id,
                    uc.User.FirstName,
                    uc.User.Bio,
                    uc.User.IsIdVerified,
                    uc.User.IsOpenToday,
                    uc.User.IsOpenToRomance,
                    uc.User.LastActiveAt,
                    uc.User.TrustScore,
                    uc.User.MeetupCount,
                    uc.User.CitiesCount,
                    uc.User.YearsNomading,
                    uc.User.CityId,
                    uc.User.CreatedAt,
                    uc.User.HangoutTags.Select(ht => ht.HangoutTagId).ToList()),
                uc.ArrivedAt))
            .ToListAsync(ct);

        await Send.OkAsync(new DiscoverArrivingResponse(rows), ct);
    }
}
