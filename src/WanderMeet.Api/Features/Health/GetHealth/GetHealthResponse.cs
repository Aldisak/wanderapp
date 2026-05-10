namespace WanderMeet.Api.Features.Health.GetHealth;

/// <summary>Liveness + dependency health payload.</summary>
/// <param name="Status">Overall status: <c>healthy</c> | <c>degraded</c> | <c>unhealthy</c>.</param>
/// <param name="Timestamp">UTC time the response was generated.</param>
/// <param name="DurationMs">Total time spent running all dependency checks, in milliseconds.</param>
/// <param name="Dependencies">Per-dependency status. Stable order matches registration order in HealthFeatureConfiguration.</param>
public record GetHealthResponse(
    string Status,
    DateTimeOffset Timestamp,
    long DurationMs,
    IReadOnlyList<HealthDependencyDto> Dependencies);

/// <summary>Status of a single dependency reported by a registered <c>IHealthCheck</c>.</summary>
/// <param name="Name">Dependency name (e.g. <c>database</c>, <c>blob-storage</c>, <c>fcm</c>).</param>
/// <param name="Status">Per-dependency status: <c>healthy</c> | <c>degraded</c> | <c>unhealthy</c>.</param>
/// <param name="DurationMs">Time the check took, in milliseconds.</param>
/// <param name="Description">Human-readable description (Healthy includes "configured" / Degraded explains why / Unhealthy carries the failure reason).</param>
public record HealthDependencyDto(
    string Name,
    string Status,
    long DurationMs,
    string? Description);
