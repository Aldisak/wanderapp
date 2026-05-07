using FluentValidation.TestHelper;
using WanderMeet.Api.Features.Discovery.Arriving;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Discovery.Arriving;

/// <summary>Unit tests for <see cref="DiscoverArrivingValidator"/>.</summary>
public class DiscoverArrivingValidatorTests
{
    private static DiscoverArrivingValidator BuildSut() => new();

    private static DiscoverArrivingRequest ValidRequest() => new()
    {
        CityId = Guid.NewGuid(),
    };

    /// <summary>Empty CityId fails with CityIdRequired error code.</summary>
    [Fact]
    public void Validate_EmptyCityId_FailsWithCityIdRequired()
    {
        var result = BuildSut().TestValidate(ValidRequest() with { CityId = Guid.Empty });
        result.ShouldHaveValidationErrorFor(x => x.CityId)
            .WithErrorCode(ErrorCodes.Validation.CityIdRequired);
    }

    /// <summary>Valid request passes without errors.</summary>
    [Fact]
    public void Validate_HappyPath_Passes()
    {
        var result = BuildSut().TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }
}
