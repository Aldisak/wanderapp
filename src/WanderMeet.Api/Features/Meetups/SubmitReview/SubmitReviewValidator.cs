using FastEndpoints;
using FluentValidation;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Meetups.SubmitReview;

/// <summary>Validates the <see cref="SubmitReviewRequest"/> input shape.</summary>
internal sealed class SubmitReviewValidator : Validator<SubmitReviewRequest>
{
    /// <summary>Initialises validation rules for <see cref="SubmitReviewRequest"/>.</summary>
    public SubmitReviewValidator()
    {
        RuleFor(x => x.Text!)
            .MaximumLength(ValidationConstants.ReviewTextMaxLength)
            .WithErrorCode(ErrorCodes.Validation.ReviewTextTooLong)
            .When(x => x.Text is not null);
    }
}
