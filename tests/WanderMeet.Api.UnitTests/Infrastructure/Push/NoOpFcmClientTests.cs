using FluentAssertions;
using Microsoft.Extensions.Logging;
using WanderMeet.Api.Infrastructure.Push;
using Xunit;

namespace WanderMeet.Api.UnitTests.Infrastructure.Push;

/// <summary>Unit tests for <see cref="NoOpFcmClient"/>.</summary>
public class NoOpFcmClientTests
{
    [Fact]
    public async Task SendAsync_ValidToken_LogsDebugAndCompletes()
    {
        // Use NullLogger to avoid FakeItEasy proxy issues with internal types.
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<NoOpFcmClient>();
        var sut = new NoOpFcmClient(logger);

        var act = async () => await sut.SendAsync("some-token", "Title", "Body", TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendAsync_AnyToken_LogsTruncatedSha256TokenHashNotRawToken()
    {
        // Arrange: use a capturing logger to verify hash-in / raw-out behaviour
        const string rawToken = "raw-fcm-token-that-must-not-be-logged";
        var capturingLogger = new CapturingLogger<NoOpFcmClient>();
        var sut = new NoOpFcmClient(capturingLogger);

        // Act
        await sut.SendAsync(rawToken, "Title", "Body", TestContext.Current.CancellationToken);

        // Assert: raw token must not appear in any log message
        capturingLogger.Messages.Should().NotContain(msg => msg.Contains(rawToken),
            because: "raw FCM token must never be logged");

        // Assert: the truncated SHA-256 hash (first 8 hex chars) should appear
        var tokenBytes = System.Text.Encoding.UTF8.GetBytes(rawToken);
        var hashBytes = System.Security.Cryptography.SHA256.HashData(tokenBytes);
        var expectedHash = Convert.ToHexString(hashBytes).ToLowerInvariant()[..8];

        capturingLogger.Messages.Should().Contain(msg => msg.Contains(expectedHash),
            because: "the first 8 hex chars of SHA-256(token) must appear in the log");
    }
}

