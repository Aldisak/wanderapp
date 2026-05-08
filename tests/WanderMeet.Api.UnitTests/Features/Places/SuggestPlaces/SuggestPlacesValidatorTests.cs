using FluentValidation.TestHelper;
using WanderMeet.Api.Features.Places.SuggestPlaces;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Places.SuggestPlaces;

/// <summary>Unit tests for <see cref="SuggestPlacesValidator"/>.</summary>
public class SuggestPlacesValidatorTests
{
    private static SuggestPlacesValidator BuildSut() => new();

    private static SuggestPlacesRequest ValidRequest() => new()
    {
        CityId = Guid.NewGuid(),
        HangoutTagSlug = null,
        Lat = 50.08,
        Lng = 14.42,
    };

    /// <summary>Empty CityId → fails with CityIdRequired.</summary>
    [Fact]
    public void Validate_EmptyCityId_FailsWithCityIdRequired()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { CityId = Guid.Empty });
        result.ShouldHaveValidationErrorFor(x => x.CityId)
            .WithErrorCode(ErrorCodes.Validation.CityIdRequired);
    }

    /// <summary>Lat=91 → fails with LatOutOfRange.</summary>
    [Fact]
    public void Validate_LatOutOfRange_FailsWithLatOutOfRange()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { Lat = 91 });
        result.ShouldHaveValidationErrorFor(x => x.Lat)
            .WithErrorCode(ErrorCodes.Validation.LatOutOfRange);
    }

    /// <summary>Lng=181 → fails with LngOutOfRange.</summary>
    [Fact]
    public void Validate_LngOutOfRange_FailsWithLngOutOfRange()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { Lng = 181 });
        result.ShouldHaveValidationErrorFor(x => x.Lng)
            .WithErrorCode(ErrorCodes.Validation.LngOutOfRange);
    }

    /// <summary>Bogus HangoutTagSlug → fails with HangoutTagSlugInvalid.</summary>
    [Fact]
    public void Validate_BogusHangoutTagSlug_FailsWithHangoutTagSlugInvalid()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { HangoutTagSlug = "bogus" });
        result.ShouldHaveValidationErrorFor(x => x.HangoutTagSlug)
            .WithErrorCode(ErrorCodes.Validation.HangoutTagSlugInvalid);
    }

    /// <summary>All-valid request → no validation errors.</summary>
    [Fact]
    public void Validate_AllValid_Passes()
    {
        var result = BuildSut().TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>Valid HangoutTagSlug → no validation errors.</summary>
    [Fact]
    public void Validate_ValidHangoutTagSlug_Passes()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { HangoutTagSlug = "Coffee" });
        result.ShouldNotHaveValidationErrorFor(x => x.HangoutTagSlug);
    }
}
