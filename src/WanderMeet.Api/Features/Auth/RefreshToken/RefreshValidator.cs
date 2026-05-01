using FastEndpoints;
using FluentValidation;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Auth.RefreshToken;

/// <summary>Validates the <see cref="RefreshRequest"/> input shape.</summary>
internal sealed class RefreshValidator : Validator<RefreshRequest>
{
    /// <summary>Initialises the validation rules.</summary>
    public RefreshValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.Validation.RefreshTokenRequired);
    }
}
