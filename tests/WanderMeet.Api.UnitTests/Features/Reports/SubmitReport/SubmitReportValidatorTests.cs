using FluentValidation.TestHelper;
using WanderMeet.Api.Features.Reports.SubmitReport;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Reports.SubmitReport;

/// <summary>Unit tests for <see cref="SubmitReportValidator"/>.</summary>
public class SubmitReportValidatorTests
{
    private readonly SubmitReportValidator _sut = new();

    // -----------------------------------------------------------------------
    // ReportedUserId
    // -----------------------------------------------------------------------

    /// <summary>Empty ReportedUserId should fail with ReportedUserIdRequired error code.</summary>
    [Fact]
    public void Validate_EmptyReportedUserId_FailsWithReportedUserIdRequired()
    {
        var result = _sut.TestValidate(new SubmitReportRequest
        {
            ReportedUserId = Guid.Empty,
            Reason = "Valid reason",
        });

        result.ShouldHaveValidationErrorFor(x => x.ReportedUserId)
            .WithErrorCode(ErrorCodes.Validation.ReportedUserIdRequired);
    }

    // -----------------------------------------------------------------------
    // Reason — required rules
    // -----------------------------------------------------------------------

    /// <summary>Null Reason should fail with ReportReasonRequired.</summary>
    [Fact]
    public void Validate_NullReason_FailsWithReportReasonRequired()
    {
        var result = _sut.TestValidate(new SubmitReportRequest
        {
            ReportedUserId = Guid.NewGuid(),
            Reason = null!,
        });

        result.ShouldHaveValidationErrorFor(x => x.Reason)
            .WithErrorCode(ErrorCodes.Validation.ReportReasonRequired);
    }

    /// <summary>Empty Reason should fail with ReportReasonRequired.</summary>
    [Fact]
    public void Validate_EmptyReason_FailsWithReportReasonRequired()
    {
        var result = _sut.TestValidate(new SubmitReportRequest
        {
            ReportedUserId = Guid.NewGuid(),
            Reason = string.Empty,
        });

        result.ShouldHaveValidationErrorFor(x => x.Reason)
            .WithErrorCode(ErrorCodes.Validation.ReportReasonRequired);
    }

    /// <summary>Whitespace-only Reason should fail with ReportReasonRequired.</summary>
    [Fact]
    public void Validate_WhitespaceOnlyReason_FailsWithReportReasonRequired()
    {
        var result = _sut.TestValidate(new SubmitReportRequest
        {
            ReportedUserId = Guid.NewGuid(),
            Reason = "   ",
        });

        result.ShouldHaveValidationErrorFor(x => x.Reason)
            .WithErrorCode(ErrorCodes.Validation.ReportReasonRequired);
    }

    // -----------------------------------------------------------------------
    // Reason — length boundary tests
    // -----------------------------------------------------------------------

    /// <summary>Reason at exactly 300 chars should pass (boundary).</summary>
    [Fact]
    public void Validate_ReasonAt300Chars_Passes()
    {
        var result = _sut.TestValidate(new SubmitReportRequest
        {
            ReportedUserId = Guid.NewGuid(),
            Reason = new string('a', ValidationConstants.ReportReasonMaxLength),
        });

        result.ShouldNotHaveValidationErrorFor(x => x.Reason);
    }

    /// <summary>Reason at 301 chars should fail with ReportReasonTooLong (boundary).</summary>
    [Fact]
    public void Validate_ReasonAt301Chars_FailsWithReportReasonTooLong()
    {
        var result = _sut.TestValidate(new SubmitReportRequest
        {
            ReportedUserId = Guid.NewGuid(),
            Reason = new string('a', ValidationConstants.ReportReasonMaxLength + 1),
        });

        result.ShouldHaveValidationErrorFor(x => x.Reason)
            .WithErrorCode(ErrorCodes.Validation.ReportReasonTooLong);
    }
}
