using FastEndpoints;
using FluentValidation;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Users.UpdateMe;

/// <summary>Validator for <see cref="UpdateMeRequest"/>.</summary>
internal sealed class UpdateMeValidator : Validator<UpdateMeRequest>
{
    /// <summary>Initialises the validation rules.</summary>
    public UpdateMeValidator()
    {
        When(x => x.Bio is not null, () =>
        {
            RuleFor(x => x.Bio)
                .MaximumLength(ValidationConstants.BioMaxLength)
                .WithErrorCode(ErrorCodes.UserValidation.BioTooLong);
        });

        When(x => x.HangoutTagIds is not null, () =>
        {
            RuleFor(x => x.HangoutTagIds!)
                .Must(ids => ids.Count <= 5)
                .WithErrorCode(ErrorCodes.UserValidation.HangoutTagIdsTooMany)
                .WithMessage("At most 5 hangout tag IDs are allowed.");

            RuleFor(x => x.HangoutTagIds!)
                .Must(ids => ids.Distinct().Count() == ids.Count)
                .WithErrorCode(ErrorCodes.UserValidation.HangoutTagIdsDuplicate)
                .WithMessage("Hangout tag IDs must be unique.");
        });
    }
}
