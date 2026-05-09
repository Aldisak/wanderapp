using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WanderMeet.Api.Infrastructure.Push;

/// <summary>
/// <see cref="IHostedService"/> that emits a startup warning through the real <see cref="ILogger"/>
/// pipeline when Firebase credentials are missing or point to a non-existent file.
/// Registering this as a hosted service ensures the warning flows through
/// Serilog / Application Insights rather than a temporary <c>LoggerFactory.Create</c>.
/// </summary>
internal sealed class FirebasePushStartupLogger(
    IOptions<FirebaseOptions> options,
    ILogger<FirebasePushStartupLogger> logger) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var credentialsPath = options.Value.CredentialsPath;

        if (string.IsNullOrWhiteSpace(credentialsPath) || !File.Exists(credentialsPath))
        {
            logger.LogWarning(
                "[FCM] Firebase credentials missing — push notifications disabled (using NoOp client).");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
