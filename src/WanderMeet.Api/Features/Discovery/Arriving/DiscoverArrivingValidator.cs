using FastEndpoints;
using FluentValidation;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Discovery.Arriving;

/// <summary>Input-shape validator for <see cref="DiscoverArrivingRequest"/>.</summary>
internal sealed class DiscoverArrivingValidator : Validator<DiscoverArrivingRequest>
{
    /// <summary>Constructs the validator with the city-id required rule.</summary>
    public DiscoverArrivingValidator()
    {
        RuleFor(x => x.CityId)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.Validation.CityIdRequired);
    }
}
