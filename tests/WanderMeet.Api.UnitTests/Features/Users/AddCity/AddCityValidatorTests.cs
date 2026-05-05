using FluentValidation.TestHelper;
using Microsoft.Extensions.Time.Testing;
using WanderMeet.Api.Features.Users.AddCity;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Users.AddCity;

/// <summary>Unit tests for <see cref="AddCityValidator"/>.</summary>
public class AddCityValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static AddCityValidator BuildSut()
    {
        var time = new FakeTimeProvider(Now);
        return new AddCityValidator(time);
    }

    [Fact]
    public void Validate_EmptyCityId_FailsWithCityIdNotFound()
    {
        var result = BuildSut().TestValidate(new AddCityRequest(Guid.Empty, Now.AddDays(-1)));
        result.ShouldHaveValidationErrorFor(x => x.CityId)
            .WithErrorCode(ErrorCodes.UserValidation.CityIdNotFound);
    }

    [Fact]
    public void Validate_ArrivedAtInFuture_FailsWithArrivedAtInFuture()
    {
        var result = BuildSut().TestValidate(new AddCityRequest(Guid.NewGuid(), Now.AddSeconds(1)));
        result.ShouldHaveValidationErrorFor(x => x.ArrivedAt)
            .WithErrorCode(ErrorCodes.UserValidation.ArrivedAtInFuture);
    }

    [Fact]
    public void Validate_ArrivedAtNow_Passes()
    {
        var result = BuildSut().TestValidate(new AddCityRequest(Guid.NewGuid(), Now));
        result.ShouldNotHaveValidationErrorFor(x => x.ArrivedAt);
    }
}
