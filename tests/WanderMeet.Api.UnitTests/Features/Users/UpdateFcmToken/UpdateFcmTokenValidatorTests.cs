using FluentAssertions;
using FluentValidation.TestHelper;
using WanderMeet.Api.Features.Users.UpdateFcmToken;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Users.UpdateFcmToken;

/// <summary>Unit tests for <see cref="UpdateFcmTokenValidator"/>.</summary>
public class UpdateFcmTokenValidatorTests
{
    private readonly UpdateFcmTokenValidator _sut = new();

    /// <summary>Null token → FcmTokenRequired error.</summary>
    [Fact]
    public void Validate_NullToken_FailsWithFcmTokenRequired()
    {
        var result = _sut.TestValidate(new UpdateFcmTokenRequest { Token = null });

        result.ShouldHaveValidationErrorFor(x => x.Token)
            .WithErrorCode(ErrorCodes.Validation.FcmTokenRequired);
    }

    /// <summary>Empty string token → FcmTokenRequired error.</summary>
    [Fact]
    public void Validate_EmptyToken_FailsWithFcmTokenRequired()
    {
        var result = _sut.TestValidate(new UpdateFcmTokenRequest { Token = "" });

        result.ShouldHaveValidationErrorFor(x => x.Token)
            .WithErrorCode(ErrorCodes.Validation.FcmTokenRequired);
    }

    /// <summary>Whitespace-only token → FcmTokenRequired error.</summary>
    [Fact]
    public void Validate_WhitespaceOnlyToken_FailsWithFcmTokenRequired()
    {
        var result = _sut.TestValidate(new UpdateFcmTokenRequest { Token = "   " });

        result.ShouldHaveValidationErrorFor(x => x.Token)
            .WithErrorCode(ErrorCodes.Validation.FcmTokenRequired);
    }

    /// <summary>512-char token → passes validation.</summary>
    [Fact]
    public void Validate_TokenAt512Chars_Passes()
    {
        var at512 = new string('a', ValidationConstants.FcmTokenMaxLength);

        var result = _sut.TestValidate(new UpdateFcmTokenRequest { Token = at512 });

        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>513-char token → FcmTokenTooLong error.</summary>
    [Fact]
    public void Validate_TokenAt513Chars_FailsWithFcmTokenTooLong()
    {
        var at513 = new string('a', ValidationConstants.FcmTokenMaxLength + 1);

        var result = _sut.TestValidate(new UpdateFcmTokenRequest { Token = at513 });

        result.ShouldHaveValidationErrorFor(x => x.Token)
            .WithErrorCode(ErrorCodes.Validation.FcmTokenTooLong);
    }
}
