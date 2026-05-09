using FluentValidation.TestHelper;
using WanderMeet.Api.Features.Meetups.SubmitReview;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Meetups.SubmitReview;

/// <summary>Unit tests for <see cref="SubmitReviewValidator"/>.</summary>
public class SubmitReviewValidatorTests
{
    private readonly SubmitReviewValidator _sut = new();

    /// <summary>Text at exactly 120 chars should pass.</summary>
    [Fact]
    public void Validate_TextAt120Chars_Passes()
    {
        var result = _sut.TestValidate(new SubmitReviewRequest
        {
            Id = Guid.NewGuid(),
            DidMeet = true,
            FeltSafe = true,
            GoodConvo = true,
            WouldMeetAgain = true,
            Text = new string('a', 120),
        });

        result.ShouldNotHaveValidationErrorFor(x => x.Text);
    }

    /// <summary>Text at 121 chars should fail with ReviewTextTooLong.</summary>
    [Fact]
    public void Validate_TextAt121Chars_FailsWithReviewTextTooLong()
    {
        var result = _sut.TestValidate(new SubmitReviewRequest
        {
            Id = Guid.NewGuid(),
            DidMeet = true,
            FeltSafe = true,
            GoodConvo = true,
            WouldMeetAgain = true,
            Text = new string('a', 121),
        });

        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorCode(ErrorCodes.Validation.ReviewTextTooLong);
    }

    /// <summary>Null text should pass (text is optional).</summary>
    [Fact]
    public void Validate_TextNull_Passes()
    {
        var result = _sut.TestValidate(new SubmitReviewRequest
        {
            Id = Guid.NewGuid(),
            DidMeet = true,
            FeltSafe = false,
            GoodConvo = false,
            WouldMeetAgain = false,
            Text = null,
        });

        result.ShouldNotHaveValidationErrorFor(x => x.Text);
    }

    /// <summary>Empty string text should pass.</summary>
    [Fact]
    public void Validate_TextEmpty_Passes()
    {
        var result = _sut.TestValidate(new SubmitReviewRequest
        {
            Id = Guid.NewGuid(),
            DidMeet = true,
            FeltSafe = false,
            GoodConvo = false,
            WouldMeetAgain = false,
            Text = string.Empty,
        });

        result.ShouldNotHaveValidationErrorFor(x => x.Text);
    }
}
