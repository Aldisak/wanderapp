using Microsoft.Extensions.Diagnostics.HealthChecks;
using WanderMeet.Api.Common;
using WanderMeet.Api.Features.Health.HealthChecks;
using WanderMeet.Api.Infrastructure.EntityFramework;

namespace WanderMeet.Api.Features.Health;

/// <summary>Feature configuration for the Health slice. Registers IHealthCheck instances
/// for every external dependency the API touches at runtime.</summary>
internal sealed class HealthFeatureConfiguration : IFeatureConfiguration
{
    /// <inheritdoc />
    public FeatureInfo Info => new("Health", "Liveness + dependency-health probe");

    /// <inheritdoc />
    public IServiceCollection AddFeatureDependencies(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            // Database: critical. Built-in EF Core check uses DbContext.Database.CanConnectAsync().
            // failureStatus=Unhealthy → API returns 503 if Postgres is down.
            .AddDbContextCheck<WanderMeetDbContext>(
                name: "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["critical"])
            // Blob storage: optional. Photo-upload endpoints already return 503 when not configured;
            // a Degraded status surfaces this without taking the whole API down.
            .AddCheck<BlobStorageHealthCheck>(
                name: "blob-storage",
                failureStatus: HealthStatus.Degraded,
                tags: ["optional"])
            // FCM: optional. NoOp client is acceptable in dev; production should be Healthy.
            .AddCheck<FcmHealthCheck>(
                name: "fcm",
                failureStatus: HealthStatus.Degraded,
                tags: ["optional"]);

        return services;
    }
}
