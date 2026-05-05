using FastEndpoints;
using FluentValidation;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Users.UpdateCity;

/// <summary>Input-shape validator for <see cref="UpdateCityRequest"/>.</summary>
internal sealed class UpdateCityValidator : Validator<UpdateCityRequest>
{
    /// <summary>Constructs the validator with all rules wired.</summary>
    public UpdateCityValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.DepartedAt!.Value)
            .LessThanOrEqualTo(_ => timeProvider.GetUtcNow())
            .WithErrorCode(ErrorCodes.UserValidation.DepartedAtInFuture)
            .When(x => x.DepartedAt.HasValue);
    }
}
