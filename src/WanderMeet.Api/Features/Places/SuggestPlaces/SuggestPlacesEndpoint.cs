using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Features.Places.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Features.Places.SuggestPlaces;

/// <summary>Returns up to 3 suggested places for a city, with sponsored-slot-3 logic.</summary>
internal sealed class SuggestPlacesEndpoint(WanderMeetDbContext dbContext)
    : Endpoint<SuggestPlacesRequest, SuggestPlacesResponse>
{
    private readonly PlacesFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Get("places/suggest");
        Description(b => b
            .WithName(nameof(SuggestPlacesEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        DontThrowIfValidationFails();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "Suggest places";
            s.Description = "Returns up to 3 suggested places for a city. Slot 3 is occupied by a sponsored place if one is available.";
            s.Responses[StatusCodes.Status200OK] = "List of suggested places (up to 3)";
            s.Responses[StatusCodes.Status400BadRequest] = "Validation error";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(SuggestPlacesRequest req, CancellationToken ct)
    {
        if (ValidationFailed)
        {
            var failures = ValidationFailures.ToList();
            ValidationFailures.Clear();
            foreach (var f in failures)
                AddError(f.ErrorCode ?? f.PropertyName, f.ErrorMessage);
            await Send.ErrorsAsync(400, ct);
            return;
        }

        // Map optional HangoutTagSlug to PlaceCategory
        PlaceCategory? matchingCategory = null;
        if (!string.IsNullOrEmpty(req.HangoutTagSlug) &&
            Enum.TryParse<HangoutTagSlug>(req.HangoutTagSlug, ignoreCase: true, out var slug))
        {
            if (HangoutTagToPlaceCategory.TryMap(slug, out var cat))
                matchingCategory = cat;
        }

        var callerPoint = new Point(req.Lng, req.Lat) { SRID = 4326 };

        // Query 1 — top 2 regular (non-sponsored) places
        var regularQuery = dbContext.Places.AsNoTracking()
            .Where(p => p.CityId == req.CityId && p.DeletedAt == null && !p.IsSponsored);

        if (matchingCategory is { } cat1)
            regularQuery = regularQuery.Where(p => p.Category == cat1);

        var regularRows = await regularQuery
            .OrderByDescending(p => p.WanderMeetupCount)
            .ThenBy(p => p.Location.Distance(callerPoint))
            .Take(2)
            .ToListAsync(ct);

        // Query 2 — top 1 sponsored place (same filters, IsSponsored == true)
        var sponsoredQuery = dbContext.Places.AsNoTracking()
            .Where(p => p.CityId == req.CityId && p.DeletedAt == null && p.IsSponsored);

        if (matchingCategory is { } cat2)
            sponsoredQuery = sponsoredQuery.Where(p => p.Category == cat2);

        var sponsored = await sponsoredQuery
            .OrderByDescending(p => p.WanderMeetupCount)
            .ThenBy(p => p.Location.Distance(callerPoint))
            .FirstOrDefaultAsync(ct);

        // Combine into slots: [regular[0]?, regular[1]?, sponsored ?? thirdRegular?]
        var result = new List<Database.Entities.Place>();

        if (regularRows.Count > 0)
            result.Add(regularRows[0]);
        if (regularRows.Count > 1)
            result.Add(regularRows[1]);

        if (sponsored is not null)
        {
            result.Add(sponsored);
        }
        else
        {
            // Fetch third regular to fill slot 3
            var thirdRegularQuery = dbContext.Places.AsNoTracking()
                .Where(p => p.CityId == req.CityId && p.DeletedAt == null && !p.IsSponsored);

            if (matchingCategory is { } cat3)
                thirdRegularQuery = thirdRegularQuery.Where(p => p.Category == cat3);

            var thirdRegular = await thirdRegularQuery
                .OrderByDescending(p => p.WanderMeetupCount)
                .ThenBy(p => p.Location.Distance(callerPoint))
                .Skip(2)
                .FirstOrDefaultAsync(ct);

            if (thirdRegular is not null)
                result.Add(thirdRegular);
        }

        // Project entities to PlaceDto in C# (PostGIS Y/X not translatable in EF projection)
        var dtos = result.Select(ToDto).ToList();

        await Send.OkAsync(new SuggestPlacesResponse(dtos), ct);
    }

    private static PlaceDto ToDto(Database.Entities.Place p) =>
        new(p.Id, p.Name, p.CityId, p.Location.Y, p.Location.X, p.Category,
            p.HasWifi, p.IsQuiet, p.IsSoloFriendly, p.GoogleRating,
            p.WanderMeetupCount, p.IsSponsored, p.SponsorPerk, p.CreatedAt);
}
