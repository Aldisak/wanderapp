namespace WanderMeet.Api.Database.Entities;

/// <summary>A user-submitted report about another user. Rate-limited to 5/day per reporter.</summary>
public class Report : AuditableEntity
{
    /// <summary>User filing the report.</summary>
    public required Guid ReporterId { get; set; }

    /// <inheritdoc cref="ReporterId" />
    public User? Reporter { get; set; }

    /// <summary>User being reported.</summary>
    public required Guid ReportedId { get; set; }

    /// <inheritdoc cref="ReportedId" />
    public User? Reported { get; set; }

    /// <summary>Free-text reason; max 300 chars.</summary>
    public required string Reason { get; set; }

    /// <summary>UTC timestamp set when a moderator triages the report; null while open.</summary>
    public DateTimeOffset? ReviewedAt { get; set; }
}
