using FastEndpoints;
using FluentValidation;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Reports.SubmitReport;

/// <summary>Validates the <see cref="SubmitReportRequest"/> input shape.</summary>
internal sealed class SubmitReportValidator : Validator<SubmitReportRequest>
{
    /// <summary>Initialises validation rules for <see cref="SubmitReportRequest"/>.</summary>
    public SubmitReportValidator()
    {
        RuleFor(x => x.ReportedUserId)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.Validation.ReportedUserIdRequired);

        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r))
            .WithErrorCode(ErrorCodes.Validation.ReportReasonRequired);

        RuleFor(x => x.Reason)
            .Must(r => r is null || r.Trim().Length <= ValidationConstants.ReportReasonMaxLength)
            .WithErrorCode(ErrorCodes.Validation.ReportReasonTooLong);
    }
}
