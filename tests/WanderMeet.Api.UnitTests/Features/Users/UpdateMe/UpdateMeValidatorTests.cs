using FluentAssertions;
using FluentValidation.TestHelper;
using WanderMeet.Api.Features.Users.UpdateMe;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Users.UpdateMe;

/// <summary>Unit tests for <see cref="UpdateMeValidator"/>.</summary>
public class UpdateMeValidatorTests
{
    private readonly UpdateMeValidator _sut = new();

    /// <summary>Bio exceeding 160 chars → BioTooLong error.</summary>
    [Fact]
    public void Validate_BioExceedsMaxLength_FailsWithBioTooLong()
    {
        var tooLong = new string('a', ValidationConstants.BioMaxLength + 1);

        var result = _sut.TestValidate(new UpdateMeRequest { Bio = tooLong });

        result.ShouldHaveValidationErrorFor(x => x.Bio)
            .WithErrorCode(ErrorCodes.UserValidation.BioTooLong);
    }

    /// <summary>More than 5 hangout tag IDs → HangoutTagIdsTooMany error.</summary>
    [Fact]
    public void Validate_HangoutTagIdsExceedsMaxCount_FailsWithTooMany()
    {
        var tooMany = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToList();

        var result = _sut.TestValidate(new UpdateMeRequest { HangoutTagIds = tooMany });

        result.ShouldHaveValidationErrorFor(x => x.HangoutTagIds)
            .WithErrorCode(ErrorCodes.UserValidation.HangoutTagIdsTooMany);
    }

    /// <summary>Duplicate hangout tag IDs → HangoutTagIdsDuplicate error.</summary>
    [Fact]
    public void Validate_HangoutTagIdsDuplicated_FailsWithDuplicate()
    {
        var duplicateId = Guid.NewGuid();
        var withDuplicates = new List<Guid> { duplicateId, Guid.NewGuid(), duplicateId };

        var result = _sut.TestValidate(new UpdateMeRequest { HangoutTagIds = withDuplicates });

        result.ShouldHaveValidationErrorFor(x => x.HangoutTagIds)
            .WithErrorCode(ErrorCodes.UserValidation.HangoutTagIdsDuplicate);
    }

    /// <summary>All fields valid (or null) → passes validation.</summary>
    [Fact]
    public void Validate_AllNullFields_Passes()
    {
        var result = _sut.TestValidate(new UpdateMeRequest());

        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>Valid bio at max length → passes validation.</summary>
    [Fact]
    public void Validate_BioAtMaxLength_Passes()
    {
        var atMax = new string('a', ValidationConstants.BioMaxLength);

        var result = _sut.TestValidate(new UpdateMeRequest { Bio = atMax });

        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>Exactly 5 unique hangout tag IDs → passes validation.</summary>
    [Fact]
    public void Validate_FiveUniqueHangoutTagIds_Passes()
    {
        var fiveIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        var result = _sut.TestValidate(new UpdateMeRequest { HangoutTagIds = fiveIds });

        result.ShouldNotHaveAnyValidationErrors();
    }
}
