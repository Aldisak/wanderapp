using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Features.Places.Shared;

/// <summary>Maps <see cref="HangoutTagSlug"/> values to <see cref="PlaceCategory"/> values for suggest filtering.</summary>
internal static class HangoutTagToPlaceCategory
{
    /// <summary>
    /// Attempts to map a <see cref="HangoutTagSlug"/> to a corresponding <see cref="PlaceCategory"/>.
    /// Returns <c>true</c> if a mapping exists; <c>false</c> otherwise.
    /// </summary>
    public static bool TryMap(HangoutTagSlug slug, out PlaceCategory category)
    {
        switch (slug)
        {
            case HangoutTagSlug.Coffee:
                category = PlaceCategory.Cafe;
                return true;
            case HangoutTagSlug.Walk:
                category = PlaceCategory.Park;
                return true;
            case HangoutTagSlug.Food:
                category = PlaceCategory.Restaurant;
                return true;
            case HangoutTagSlug.Explore:
                category = PlaceCategory.Landmark;
                return true;
            case HangoutTagSlug.Cowork:
                category = PlaceCategory.Cowork;
                return true;
            default:
                category = default;
                return false;
        }
    }
}
