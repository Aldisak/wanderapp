namespace WanderMeet.Api.Features.Reports.SubmitReport;

/// <summary>Request body for POST /api/v1/reports (submit a user report).</summary>
public record SubmitReportRequest
{
    /// <summary>Id of the user being reported.</summary>
    public required Guid ReportedUserId { get; init; }

    /// <summary>Free-text reason for the report (max 300 chars after trimming).</summary>
    public required string Reason { get; init; }
}
