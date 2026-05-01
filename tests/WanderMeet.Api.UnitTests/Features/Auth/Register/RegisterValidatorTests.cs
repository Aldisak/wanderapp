using FluentAssertions;
using FluentValidation.TestHelper;
using WanderMeet.Api.Features.Auth.Register;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Auth.Register;

/// <summary>Unit tests for <see cref="RegisterValidator"/>.</summary>
public class RegisterValidatorTests
{
    private readonly RegisterValidator _sut = new();

    /// <summary>Empty FirstName should fail with the FirstNameRequired error code.</summary>
    [Fact]
    public void Validate_FirstNameEmpty_FailsWithValidationFirstNameRequired()
    {
        var result = _sut.TestValidate(new RegisterRequest { FirstName = string.Empty });

        result.ShouldHaveValidationErrorFor(x => x.FirstName)
            .WithErrorCode(ErrorCodes.Validation.FirstNameRequired);
    }

    /// <summary>Whitespace-only FirstName should fail with the FirstNameRequired error code.</summary>
    [Fact]
    public void Validate_FirstNameWhitespace_FailsWithValidationFirstNameRequired()
    {
        var result = _sut.TestValidate(new RegisterRequest { FirstName = "   " });

        result.ShouldHaveValidationErrorFor(x => x.FirstName)
            .WithErrorCode(ErrorCodes.Validation.FirstNameRequired);
    }

    /// <summary>FirstName over max length should fail with the FirstNameTooLong error code.</summary>
    [Fact]
    public void Validate_FirstNameExceedsMaxLength_FailsWithValidationFirstNameTooLong()
    {
        var tooLong = new string('a', ValidationConstants.FirstNameMaxLength + 1);

        var result = _sut.TestValidate(new RegisterRequest { FirstName = tooLong });

        result.ShouldHaveValidationErrorFor(x => x.FirstName)
            .WithErrorCode(ErrorCodes.Validation.FirstNameTooLong);
    }

    /// <summary>FirstName exactly at max length should pass validation.</summary>
    [Fact]
    public void Validate_FirstNameAtMaxLength_Passes()
    {
        var atMax = new string('a', ValidationConstants.FirstNameMaxLength);

        var result = _sut.TestValidate(new RegisterRequest { FirstName = atMax });

        result.ShouldNotHaveValidationErrorFor(x => x.FirstName);
    }
}
