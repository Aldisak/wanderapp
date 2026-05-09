using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace WanderMeet.Api.Infrastructure.Push;

/// <summary>
/// No-op implementation of <see cref="IFcmClient"/> used when Firebase credentials are absent.
/// Logs at Debug level with a SHA-256-truncated token hash — never the raw token.
/// </summary>
internal sealed class NoOpFcmClient(ILogger<NoOpFcmClient> logger) : IFcmClient
{
    /// <inheritdoc />
    public Task SendAsync(string fcmToken, string title, string body, CancellationToken ct)
    {
        var tokenHash = ComputeTokenHash(fcmToken);
        logger.LogDebug(
            "NoOpFcmClient: skipping push title={Title} body={Body} tokenHash={TokenHash}",
            title, body, tokenHash);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns the first 8 lowercase hex characters of SHA-256(fcmToken encoded as UTF-8).
    /// The raw token is never logged or stored.
    /// </summary>
    private static string ComputeTokenHash(string fcmToken)
    {
        var bytes = Encoding.UTF8.GetBytes(fcmToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant()[..8];
    }
}
