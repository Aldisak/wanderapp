using FluentValidation.TestHelper;
using WanderMeet.Api.Features.Places.ListPlaces;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Places.ListPlaces;

/// <summary>Unit tests for <see cref="ListPlacesValidator"/>.</summary>
public class ListPlacesValidatorTests
{
    private static ListPlacesValidator BuildSut() => new();

    private static ListPlacesRequest ValidRequest() => new()
    {
        CityId = Guid.NewGuid(),
        Category = null,
    };

    /// <summary>Empty CityId → fails with CityIdRequired.</summary>
    [Fact]
    public void Validate_EmptyCityId_FailsWithCityIdRequired()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { CityId = Guid.Empty });
        result.ShouldHaveValidationErrorFor(x => x.CityId)
            .WithErrorCode(ErrorCodes.Validation.CityIdRequired);
    }

    /// <summary>Bogus category → fails with PlaceCategoryInvalid.</summary>
    [Fact]
    public void Validate_BogusCategory_FailsWithPlaceCategoryInvalid()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { Category = "bogus" });
        result.ShouldHaveValidationErrorFor(x => x.Category)
            .WithErrorCode(ErrorCodes.Validation.PlaceCategoryInvalid);
    }

    /// <summary>All-valid request (no category) → no validation errors.</summary>
    [Fact]
    public void Validate_AllValid_NoCategory_Passes()
    {
        var result = BuildSut().TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>Valid category string → no validation errors.</summary>
    [Fact]
    public void Validate_ValidCategory_Passes()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { Category = "Cafe" });
        result.ShouldNotHaveValidationErrorFor(x => x.Category);
    }
}
