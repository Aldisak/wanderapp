using Microsoft.Extensions.Diagnostics.HealthChecks;
using WanderMeet.Infrastructure.Blob;

namespace WanderMeet.Api.Features.Health.HealthChecks;

/// <summary>
/// Reports the Azure Blob Storage dependency as <see cref="HealthStatus.Healthy"/> when a
/// connection string is configured, and as <see cref="HealthStatus.Degraded"/> otherwise
/// (NoOp mode — photo upload endpoints will return 503 but the rest of the API is fine).
/// </summary>
internal sealed class BlobStorageHealthCheck(IBlobStorageService blobStorage) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!blobStorage.IsConfigured)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "Blob storage is not configured (BlobStorage:ConnectionString missing). Photo upload endpoints will return 503."));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Blob storage configured."));
    }
}
