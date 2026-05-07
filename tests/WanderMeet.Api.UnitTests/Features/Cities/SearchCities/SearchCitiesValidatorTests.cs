using FluentValidation.TestHelper;
using WanderMeet.Api.Features.Cities.SearchCities;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Cities.SearchCities;

/// <summary>Unit tests for <see cref="SearchCitiesValidator"/>.</summary>
public class SearchCitiesValidatorTests
{
    private static SearchCitiesValidator BuildSut() => new();

    private static SearchCitiesRequest ValidRequest() => new() { Q = "Lis", Limit = 20 };

    /// <summary>Empty query string → fails with SearchQueryTooShort.</summary>
    [Fact]
    public void Validate_QueryEmpty_FailsWithSearchQueryTooShort()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { Q = string.Empty });
        result.ShouldHaveValidationErrorFor(x => x.Q)
            .WithErrorCode(ErrorCodes.Validation.SearchQueryTooShort);
    }

    /// <summary>Single-char query → fails with SearchQueryTooShort.</summary>
    [Fact]
    public void Validate_QueryOneChar_FailsWithSearchQueryTooShort()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { Q = "L" });
        result.ShouldHaveValidationErrorFor(x => x.Q)
            .WithErrorCode(ErrorCodes.Validation.SearchQueryTooShort);
    }

    /// <summary>Exactly two chars → passes.</summary>
    [Fact]
    public void Validate_QueryTwoChars_Passes()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { Q = "Li" });
        result.ShouldNotHaveValidationErrorFor(x => x.Q);
    }

    /// <summary>121-char query → fails with SearchQueryTooLong.</summary>
    [Fact]
    public void Validate_Query121Chars_FailsWithSearchQueryTooLong()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { Q = new string('A', 121) });
        result.ShouldHaveValidationErrorFor(x => x.Q)
            .WithErrorCode(ErrorCodes.Validation.SearchQueryTooLong);
    }

    /// <summary>Limit zero → fails with LimitOutOfRange.</summary>
    [Fact]
    public void Validate_LimitZero_FailsWithLimitOutOfRange()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { Limit = 0 });
        result.ShouldHaveValidationErrorFor(x => x.Limit)
            .WithErrorCode(ErrorCodes.Validation.LimitOutOfRange);
    }

    /// <summary>Limit 51 → fails with LimitOutOfRange.</summary>
    [Fact]
    public void Validate_Limit51_FailsWithLimitOutOfRange()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { Limit = 51 });
        result.ShouldHaveValidationErrorFor(x => x.Limit)
            .WithErrorCode(ErrorCodes.Validation.LimitOutOfRange);
    }

    /// <summary>Limit 20 → passes.</summary>
    [Fact]
    public void Validate_Limit20_Passes()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { Limit = 20 });
        result.ShouldNotHaveValidationErrorFor(x => x.Limit);
    }
}
