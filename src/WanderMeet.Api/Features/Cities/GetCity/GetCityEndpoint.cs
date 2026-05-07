using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Features.Cities.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Cities.GetCity;

/// <summary>Returns detailed information for a single city, including the count of currently active nomads.</summary>
internal sealed class GetCityEndpoint(WanderMeetDbContext dbContext, TimeProvider timeProvider)
    : Endpoint<GetCityRequest, CityDetailDto>
{
    private readonly CitiesFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Get("cities/{id:guid}");
        Description(b => b
            .WithName(nameof(GetCityEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "Get city detail";
            s.Description = "Returns city data plus the count of users currently in this city who are open today and recently active.";
            s.Responses[StatusCodes.Status200OK] = "City detail with active nomad count";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "City not found or soft-deleted";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(GetCityRequest req, CancellationToken ct)
    {
        var activeSince = timeProvider.GetUtcNow() - ValidationConstants.DiscoveryActiveWindow;

        // Materialise the city first; ST_Y/ST_X not available on geography columns in a single projection.
        var city = await dbContext.Cities
            .AsNoTracking()
            .Where(c => c.Id == req.Id && c.DeletedAt == null)
            .FirstOrDefaultAsync(ct);

        if (city is null)
        {
            AddError(ErrorCodes.City.NotFound, "City not found.");
            await Send.ErrorsAsync(404, ct);
            return;
        }

        var activeNomadCount = await dbContext.Users
            .AsNoTracking()
            .CountAsync(u =>
                u.CityId == city.Id &&
                u.IsOpenToday &&
                u.LastActiveAt > activeSince &&
                u.DeletedAt == null,
                ct);

        var cityDto = new CityDto(city.Id, city.Name, city.Country, city.Location.Y, city.Location.X, city.CreatedAt);
        var dto = new CityDetailDto(cityDto, activeNomadCount);

        await Send.OkAsync(dto, ct);
    }
}
