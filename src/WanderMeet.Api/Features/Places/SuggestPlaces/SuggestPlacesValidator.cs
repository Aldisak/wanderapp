using FastEndpoints;
using FluentValidation;
using WanderMeet.Shared;
using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Features.Places.SuggestPlaces;

/// <summary>Validates the <see cref="SuggestPlacesRequest"/> query parameters.</summary>
internal sealed class SuggestPlacesValidator : Validator<SuggestPlacesRequest>
{
    /// <summary>Initialises the validation rules.</summary>
    public SuggestPlacesValidator()
    {
        RuleFor(x => x.CityId)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.Validation.CityIdRequired);

        When(x => !string.IsNullOrEmpty(x.HangoutTagSlug), () =>
        {
            RuleFor(x => x.HangoutTagSlug)
                .Must(slug => Enum.TryParse<HangoutTagSlug>(slug, ignoreCase: true, out _))
                .WithErrorCode(ErrorCodes.Validation.HangoutTagSlugInvalid);
        });

        RuleFor(x => x.Lat)
            .InclusiveBetween(-90, 90)
            .WithErrorCode(ErrorCodes.Validation.LatOutOfRange);

        RuleFor(x => x.Lng)
            .InclusiveBetween(-180, 180)
            .WithErrorCode(ErrorCodes.Validation.LngOutOfRange);
    }
}
