using FluentValidation.TestHelper;
using WanderMeet.Api.Features.Users.UploadPhoto;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Users.UploadPhoto;

/// <summary>Unit tests for <see cref="UploadPhotoValidator"/>.</summary>
public class UploadPhotoValidatorTests
{
    private static UploadPhotoValidator BuildSut() => new();

    /// <summary>Order below 0 fails with PhotoOrderOutOfRange.</summary>
    [Fact]
    public void Validate_OrderEqualsMinusOne_FailsWithPhotoOrderOutOfRange()
    {
        var result = BuildSut().TestValidate(new UploadPhotoRequest(-1));
        result.ShouldHaveValidationErrorFor(x => x.Order)
            .WithErrorCode(ErrorCodes.Validation.PhotoOrderOutOfRange);
    }

    /// <summary>Order above MaxPhotosPerUser-1 (i.e. 4) fails with PhotoOrderOutOfRange.</summary>
    [Fact]
    public void Validate_OrderEqualsFour_FailsWithPhotoOrderOutOfRange()
    {
        var result = BuildSut().TestValidate(new UploadPhotoRequest(4));
        result.ShouldHaveValidationErrorFor(x => x.Order)
            .WithErrorCode(ErrorCodes.Validation.PhotoOrderOutOfRange);
    }

    /// <summary>Order == 0 passes validation.</summary>
    [Fact]
    public void Validate_OrderEqualsZero_Passes()
    {
        var result = BuildSut().TestValidate(new UploadPhotoRequest(0));
        result.ShouldNotHaveValidationErrorFor(x => x.Order);
    }

    /// <summary>Order == 3 (MaxPhotosPerUser - 1) passes validation.</summary>
    [Fact]
    public void Validate_OrderEqualsThree_Passes()
    {
        var result = BuildSut().TestValidate(new UploadPhotoRequest(3));
        result.ShouldNotHaveValidationErrorFor(x => x.Order);
    }
}
