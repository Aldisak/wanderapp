using FastEndpoints;
using FluentValidation;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Users.UpdateFcmToken;

/// <summary>Validates the FCM token update request.</summary>
internal sealed class UpdateFcmTokenValidator : Validator<UpdateFcmTokenRequest>
{
    /// <summary>Initialises the validator rules.</summary>
    public UpdateFcmTokenValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.Validation.FcmTokenRequired);

        RuleFor(x => x.Token)
            .MaximumLength(ValidationConstants.FcmTokenMaxLength)
            .WithErrorCode(ErrorCodes.Validation.FcmTokenTooLong)
            .When(x => !string.IsNullOrEmpty(x.Token));
    }
}
