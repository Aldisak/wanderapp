using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Features.Cities.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;

namespace WanderMeet.Api.Features.Cities.SearchCities;

/// <summary>Searches cities by name using a case-insensitive partial match.</summary>
internal sealed class SearchCitiesEndpoint(WanderMeetDbContext dbContext)
    : Endpoint<SearchCitiesRequest, SearchCitiesResponse>
{
    private readonly CitiesFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Get("cities/search");
        Description(b => b
            .WithName(nameof(SearchCitiesEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        DontThrowIfValidationFails();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "Search cities";
            s.Description = "Returns cities whose name contains the search term (case-insensitive).";
            s.Responses[StatusCodes.Status200OK] = "List of matching cities";
            s.Responses[StatusCodes.Status400BadRequest] = "Validation error (query too short/long, limit out of range)";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(SearchCitiesRequest req, CancellationToken ct)
    {
        if (ValidationFailed)
        {
            // Re-emit FluentValidation failures using ErrorCode as the JSON property key,
            // so the stable error code appears in the HTTP response body.
            var failures = ValidationFailures.ToList();
            ValidationFailures.Clear();
            foreach (var f in failures)
                AddError(f.ErrorCode ?? f.PropertyName, f.ErrorMessage);
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var q = req.Q;

        // Project non-spatial fields in SQL; extract lat/lon from Point in C#
        // ST_Y/ST_X do not work on geography columns — materialise the entity first.
        var cities = await dbContext.Cities
            .AsNoTracking()
            .Where(c => c.DeletedAt == null && EF.Functions.ILike(c.Name, $"%{q}%"))
            .OrderBy(c => c.Name)
            .Take(req.Limit)
            .ToListAsync(ct);

        var rows = cities.Select(c => new CityDto(c.Id, c.Name, c.Country, c.Location.Y, c.Location.X, c.CreatedAt))
            .ToList();

        await Send.OkAsync(new SearchCitiesResponse(rows), ct);
    }
}
