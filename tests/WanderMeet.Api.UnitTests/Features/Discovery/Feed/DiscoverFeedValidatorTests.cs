using FluentAssertions;
using FluentValidation.TestHelper;
using WanderMeet.Api.Features.Discovery.Feed;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Discovery.Feed;

/// <summary>Unit tests for <see cref="DiscoverFeedValidator"/>.</summary>
public class DiscoverFeedValidatorTests
{
    private static DiscoverFeedValidator BuildSut() => new();

    private static DiscoverFeedRequest ValidRequest() => new()
    {
        CityId = Guid.NewGuid(),
        Limit = 20,
    };

    [Fact]
    public void Validate_EmptyCityId_FailsWithCityIdRequired()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { CityId = Guid.Empty });
        result.ShouldHaveValidationErrorFor(x => x.CityId)
            .WithErrorCode(ErrorCodes.Validation.CityIdRequired);
    }

    [Fact]
    public void Validate_LimitBelowOne_FailsWithLimitOutOfRange()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { Limit = 0 });
        result.ShouldHaveValidationErrorFor(x => x.Limit)
            .WithErrorCode(ErrorCodes.Validation.LimitOutOfRange);
    }

    [Fact]
    public void Validate_LimitAboveFifty_FailsWithLimitOutOfRange()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { Limit = 51 });
        result.ShouldHaveValidationErrorFor(x => x.Limit)
            .WithErrorCode(ErrorCodes.Validation.LimitOutOfRange);
    }

    [Fact]
    public void Validate_LimitOmitted_DefaultsToTwentyAndPasses()
    {
        var req = new DiscoverFeedRequest { CityId = Guid.NewGuid() };
        req.Limit.Should().Be(20);
        var result = BuildSut().TestValidate(req);
        result.ShouldNotHaveValidationErrorFor(x => x.Limit);
    }

    [Fact]
    public void Validate_HangoutTagSlugUnknown_FailsWithHangoutTagSlugInvalid()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { HangoutTagSlug = "NotASlug" });
        result.ShouldHaveValidationErrorFor(x => x.HangoutTagSlug)
            .WithErrorCode(ErrorCodes.Validation.HangoutTagSlugInvalid);
    }

    [Fact]
    public void Validate_CursorMalformedBase64_FailsWithCursorMalformed()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { Cursor = "not!valid!base64" });
        result.ShouldHaveValidationErrorFor(x => x.Cursor)
            .WithErrorCode(ErrorCodes.Validation.CursorMalformed);
    }

    [Fact]
    public void Validate_CursorBase64ButNotJsonShape_FailsWithCursorMalformed()
    {
        var wrongShapeCursor = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"foo\":\"bar\"}"));
        var result = BuildSut().TestValidate(ValidRequest() with { Cursor = wrongShapeCursor });
        result.ShouldHaveValidationErrorFor(x => x.Cursor)
            .WithErrorCode(ErrorCodes.Validation.CursorMalformed);
    }

    [Fact]
    public void Validate_HappyPath_Passes()
    {
        var result = BuildSut().TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_NullHangoutTagSlug_Passes()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { HangoutTagSlug = null });
        result.ShouldNotHaveValidationErrorFor(x => x.HangoutTagSlug);
    }

    [Fact]
    public void Validate_ValidHangoutTagSlug_Passes()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { HangoutTagSlug = "Coffee" });
        result.ShouldNotHaveValidationErrorFor(x => x.HangoutTagSlug);
    }

    [Fact]
    public void Validate_ValidCursor_Passes()
    {
        var cursor = new DiscoveryCursor(DateTimeOffset.UtcNow, 50, Guid.NewGuid(), true);
        var encoded = DiscoveryCursor.Encode(cursor);
        var result = BuildSut().TestValidate(ValidRequest() with { Cursor = encoded });
        result.ShouldNotHaveValidationErrorFor(x => x.Cursor);
    }
}
