using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WanderMeet.Api.Common;

namespace WanderMeet.Api.Features.Health.GetHealth;

/// <summary>
/// Liveness + dependency health probe. Anonymous on purpose so Container Apps probes
/// and uptime monitors can reach it. Status code is 200 for Healthy/Degraded and 503
/// for Unhealthy (any critical dependency — currently only the database — is down).
/// </summary>
internal sealed class GetHealthEndpoint(
    HealthCheckService healthCheckService,
    TimeProvider timeProvider) : EndpointWithoutRequest<GetHealthResponse>
{
    private readonly HealthFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Get("health");
        AllowAnonymous();
        Description(builder => builder
            .WithName(nameof(GetHealthEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();

        Summary(s =>
        {
            s.Summary = "Liveness + dependency health probe";
            s.Description =
                "Aggregates registered IHealthCheck results (database, blob-storage, fcm). " +
                "Anonymous on purpose — Azure Container Apps health probes and uptime monitors hit this. " +
                "Returns 200 with status=healthy/degraded when the API is serving traffic; returns 503 when a critical dependency is down.";
            s.Responses[StatusCodes.Status200OK] = "All critical dependencies up (status=healthy or degraded)";
            s.Responses[StatusCodes.Status503ServiceUnavailable] = "A critical dependency (database) is unhealthy";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(CancellationToken ct)
    {
        var report = await healthCheckService.CheckHealthAsync(ct);

        var dependencies = report.Entries
            .Select(kv => new HealthDependencyDto(
                Name: kv.Key,
                Status: ToWireStatus(kv.Value.Status),
                DurationMs: (long)kv.Value.Duration.TotalMilliseconds,
                Description: kv.Value.Description ?? kv.Value.Exception?.Message))
            .ToList();

        var response = new GetHealthResponse(
            Status: ToWireStatus(report.Status),
            Timestamp: timeProvider.GetUtcNow(),
            DurationMs: (long)report.TotalDuration.TotalMilliseconds,
            Dependencies: dependencies);

        var statusCode = report.Status == HealthStatus.Unhealthy
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK;

        await Send.ResponseAsync(response, statusCode, ct);
    }

    private static string ToWireStatus(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "healthy",
        HealthStatus.Degraded => "degraded",
        HealthStatus.Unhealthy => "unhealthy",
        _ => "unknown",
    };
}
