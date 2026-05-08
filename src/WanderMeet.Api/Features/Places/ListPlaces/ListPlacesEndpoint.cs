using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Features.Places.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Features.Places.ListPlaces;

/// <summary>Returns all places in a city, optionally filtered by category.</summary>
internal sealed class ListPlacesEndpoint(WanderMeetDbContext dbContext)
    : Endpoint<ListPlacesRequest, ListPlacesResponse>
{
    private readonly PlacesFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Get("places");
        Description(b => b
            .WithName(nameof(ListPlacesEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        DontThrowIfValidationFails();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "List places";
            s.Description = "Returns all places in a city ordered alphabetically, optionally filtered by category.";
            s.Responses[StatusCodes.Status200OK] = "List of places";
            s.Responses[StatusCodes.Status400BadRequest] = "Validation error";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(ListPlacesRequest req, CancellationToken ct)
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

        PlaceCategory? categoryEnum = null;
        if (!string.IsNullOrEmpty(req.Category) &&
            Enum.TryParse<PlaceCategory>(req.Category, ignoreCase: true, out var parsedCategory))
        {
            categoryEnum = parsedCategory;
        }

        var query = dbContext.Places.AsNoTracking()
            .Where(p => p.CityId == req.CityId && p.DeletedAt == null);

        if (categoryEnum is { } cat)
            query = query.Where(p => p.Category == cat);

        var places = await query.OrderBy(p => p.Name).ToListAsync(ct);

        // Project in C# — PostGIS Y/X not translatable in EF projection
        var dtos = places.Select(ToDto).ToList();

        await Send.OkAsync(new ListPlacesResponse(dtos), ct);
    }

    private static PlaceDto ToDto(Database.Entities.Place p) =>
        new(p.Id, p.Name, p.CityId, p.Location.Y, p.Location.X, p.Category,
            p.HasWifi, p.IsQuiet, p.IsSoloFriendly, p.GoogleRating,
            p.WanderMeetupCount, p.IsSponsored, p.SponsorPerk, p.CreatedAt);
}
