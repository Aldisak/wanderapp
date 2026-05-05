using FastEndpoints;
using FluentValidation;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Users.AddCity;

/// <summary>Input-shape validator for <see cref="AddCityRequest"/>.</summary>
internal sealed class AddCityValidator : Validator<AddCityRequest>
{
    /// <summary>Constructs the validator with all rules wired.</summary>
    public AddCityValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.CityId)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.UserValidation.CityIdNotFound);

        RuleFor(x => x.ArrivedAt)
            .LessThanOrEqualTo(_ => timeProvider.GetUtcNow())
            .WithErrorCode(ErrorCodes.UserValidation.ArrivedAtInFuture);
    }
}
