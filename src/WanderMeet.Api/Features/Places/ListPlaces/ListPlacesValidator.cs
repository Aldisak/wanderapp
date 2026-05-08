using FastEndpoints;
using FluentValidation;
using WanderMeet.Shared;
using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Features.Places.ListPlaces;

/// <summary>Validates the <see cref="ListPlacesRequest"/> query parameters.</summary>
internal sealed class ListPlacesValidator : Validator<ListPlacesRequest>
{
    /// <summary>Initialises the validation rules.</summary>
    public ListPlacesValidator()
    {
        RuleFor(x => x.CityId)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.Validation.CityIdRequired);

        When(x => !string.IsNullOrEmpty(x.Category), () =>
        {
            RuleFor(x => x.Category)
                .Must(cat => Enum.TryParse<PlaceCategory>(cat, ignoreCase: true, out _))
                .WithErrorCode(ErrorCodes.Validation.PlaceCategoryInvalid);
        });
    }
}
