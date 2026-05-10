using Microsoft.Extensions.Diagnostics.HealthChecks;
using WanderMeet.Api.Infrastructure.Push;

namespace WanderMeet.Api.Features.Health.HealthChecks;

/// <summary>
/// Reports the FCM push transport as <see cref="HealthStatus.Healthy"/> when a real
/// Firebase client is registered, and as <see cref="HealthStatus.Degraded"/> when the
/// NoOp client is in use (push notifications silently dropped — invite + meetup flows
/// still work but mobile clients won't receive background pushes).
/// </summary>
internal sealed class FcmHealthCheck(IFcmClient fcmClient) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (fcmClient is NoOpFcmClient)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "FCM is running in NoOp mode (Firebase:CredentialsPath missing or file not found). Push notifications are disabled."));
        }

        return Task.FromResult(HealthCheckResult.Healthy("FCM client configured."));
    }
}
