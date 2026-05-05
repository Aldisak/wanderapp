using FluentValidation.TestHelper;
using Microsoft.Extensions.Time.Testing;
using WanderMeet.Api.Features.Users.UpdateCity;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Users.UpdateCity;

/// <summary>Unit tests for <see cref="UpdateCityValidator"/>.</summary>
public class UpdateCityValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static UpdateCityValidator BuildSut()
    {
        var time = new FakeTimeProvider(Now);
        return new UpdateCityValidator(time);
    }

    [Fact]
    public void Validate_DepartedAtInFuture_FailsWithDepartedAtInFuture()
    {
        var result = BuildSut().TestValidate(new UpdateCityRequest { Id = Guid.NewGuid(), DepartedAt = Now.AddDays(1) });
        result.ShouldHaveValidationErrorFor(x => x.DepartedAt!.Value)
            .WithErrorCode(ErrorCodes.UserValidation.DepartedAtInFuture);
    }

    [Fact]
    public void Validate_DepartedAtNull_Passes()
    {
        var result = BuildSut().TestValidate(new UpdateCityRequest { Id = Guid.NewGuid(), DepartedAt = null });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_DepartedAtPast_Passes()
    {
        var result = BuildSut().TestValidate(new UpdateCityRequest { Id = Guid.NewGuid(), DepartedAt = Now.AddDays(-1) });
        result.ShouldNotHaveAnyValidationErrors();
    }
}
