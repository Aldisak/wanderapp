namespace WanderMeet.Api.Features.Health.GetHealth;

/// <summary>Liveness probe payload.</summary>
/// <param name="Status">Always <c>"healthy"</c> when the API process is running.</param>
/// <param name="Timestamp">UTC time the response was generated.</param>
public record GetHealthResponse(string Status, DateTimeOffset Timestamp);
