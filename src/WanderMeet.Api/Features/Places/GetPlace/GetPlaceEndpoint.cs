using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Features.Places.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Places.GetPlace;

/// <summary>Returns a single place by its id.</summary>
internal sealed class GetPlaceEndpoint(WanderMeetDbContext dbContext)
    : Endpoint<GetPlaceRequest, PlaceDto>
{
    private readonly PlacesFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Get("places/{id:guid}");
        Description(b => b
            .WithName(nameof(GetPlaceEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "Get place detail";
            s.Description = "Returns a single place by its id.";
            s.Responses[StatusCodes.Status200OK] = "Place detail";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "Place not found or soft-deleted";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(GetPlaceRequest req, CancellationToken ct)
    {
        // Materialise first — PostGIS Y/X not translatable in EF projection on geography columns
        var place = await dbContext.Places.AsNoTracking()
            .Where(p => p.Id == req.Id && p.DeletedAt == null)
            .FirstOrDefaultAsync(ct);

        if (place is null)
        {
            AddError(ErrorCodes.Place.NotFound, "Place not found.");
            await Send.ErrorsAsync(404, ct);
            return;
        }

        var dto = new PlaceDto(
            place.Id, place.Name, place.CityId, place.Location.Y, place.Location.X,
            place.Category, place.HasWifi, place.IsQuiet, place.IsSoloFriendly,
            place.GoogleRating, place.WanderMeetupCount, place.IsSponsored,
            place.SponsorPerk, place.CreatedAt);

        await Send.OkAsync(dto, ct);
    }
}
