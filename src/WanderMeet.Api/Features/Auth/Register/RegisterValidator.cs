using FastEndpoints;
using FluentValidation;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Auth.Register;

/// <summary>Input validator for <see cref="RegisterRequest"/>.</summary>
internal sealed class RegisterValidator : Validator<RegisterRequest>
{
    /// <summary>Initialises validation rules for registration.</summary>
    public RegisterValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.Validation.FirstNameRequired);

        RuleFor(x => x.FirstName)
            .MaximumLength(ValidationConstants.FirstNameMaxLength)
            .WithErrorCode(ErrorCodes.Validation.FirstNameTooLong);
    }
}
