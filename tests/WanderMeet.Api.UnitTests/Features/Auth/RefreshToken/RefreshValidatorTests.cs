using FluentValidation.TestHelper;
using WanderMeet.Api.Features.Auth.RefreshToken;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Auth.RefreshToken;

/// <summary>Unit tests for <see cref="RefreshValidator"/>.</summary>
public class RefreshValidatorTests
{
    private readonly RefreshValidator _sut = new();

    /// <summary>Empty RefreshToken should fail with the RefreshTokenRequired error code.</summary>
    [Fact]
    public void Validate_RefreshTokenEmpty_FailsWithValidationRefreshTokenRequired()
    {
        var result = _sut.TestValidate(new RefreshRequest { RefreshToken = string.Empty });

        result.ShouldHaveValidationErrorFor(x => x.RefreshToken)
            .WithErrorCode(ErrorCodes.Validation.RefreshTokenRequired);
    }

    /// <summary>Whitespace-only RefreshToken should fail with the RefreshTokenRequired error code.</summary>
    [Fact]
    public void Validate_RefreshTokenWhitespace_FailsWithValidationRefreshTokenRequired()
    {
        var result = _sut.TestValidate(new RefreshRequest { RefreshToken = "   " });

        result.ShouldHaveValidationErrorFor(x => x.RefreshToken)
            .WithErrorCode(ErrorCodes.Validation.RefreshTokenRequired);
    }

    /// <summary>Valid non-empty RefreshToken should pass validation.</summary>
    [Fact]
    public void Validate_RefreshTokenProvided_Passes()
    {
        var result = _sut.TestValidate(new RefreshRequest { RefreshToken = "some-refresh-token" });

        result.ShouldNotHaveValidationErrorFor(x => x.RefreshToken);
    }
}
