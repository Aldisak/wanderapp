using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WanderMeet.Api.Infrastructure.Push;
using Xunit;

namespace WanderMeet.Api.UnitTests.Infrastructure.Push;

/// <summary>Unit tests for <see cref="FirebasePushStartupLogger"/>.</summary>
public class FirebasePushStartupLoggerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StartAsync_CredentialsMissingOrEmptyOrWhitespace_LogsWarning(string? credentialsPath)
    {
        var capturingLogger = new CapturingLogger<FirebasePushStartupLogger>();
        var options = Options.Create(new FirebaseOptions { CredentialsPath = credentialsPath });
        var sut = new FirebasePushStartupLogger(options, capturingLogger);

        await sut.StartAsync(TestContext.Current.CancellationToken);

        capturingLogger.LogEntries.Should().Contain(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("[FCM] Firebase credentials missing — push notifications disabled (using NoOp client)."),
            because: "missing credentials must emit the exact warning text through the real ILogger pipeline");
    }

    [Fact]
    public async Task StartAsync_CredentialsPathNonExistentFile_LogsWarning()
    {
        const string nonExistentPath = "/tmp/does-not-exist-fcm-credentials.json";
        var capturingLogger = new CapturingLogger<FirebasePushStartupLogger>();
        var options = Options.Create(new FirebaseOptions { CredentialsPath = nonExistentPath });
        var sut = new FirebasePushStartupLogger(options, capturingLogger);

        await sut.StartAsync(TestContext.Current.CancellationToken);

        capturingLogger.LogEntries.Should().Contain(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("[FCM] Firebase credentials missing — push notifications disabled (using NoOp client)."),
            because: "a path that does not exist must emit the exact warning text");
    }

    [Fact]
    public async Task StartAsync_CredentialsPresentAndFileExists_DoesNotLogWarning()
    {
        // Create a temp file that actually exists
        var tempFile = Path.GetTempFileName();
        try
        {
            var capturingLogger = new CapturingLogger<FirebasePushStartupLogger>();
            var options = Options.Create(new FirebaseOptions { CredentialsPath = tempFile });
            var sut = new FirebasePushStartupLogger(options, capturingLogger);

            await sut.StartAsync(TestContext.Current.CancellationToken);

            capturingLogger.LogEntries.Should().NotContain(entry => entry.Level == LogLevel.Warning,
                because: "when a valid credentials file exists, no warning should be emitted");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task StopAsync_Always_ReturnsCompleted()
    {
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<FirebasePushStartupLogger>();
        var options = Options.Create(new FirebaseOptions());
        var sut = new FirebasePushStartupLogger(options, logger);

        // Should not throw
        await sut.StopAsync(TestContext.Current.CancellationToken);
    }
}

/// <summary>Captures log entries including level and formatted message for assertions.</summary>
internal sealed record CapturedLogEntry(LogLevel Level, string Message);

/// <summary>Logger that captures entries with their log level for assertions.</summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    /// <summary>All log entries captured since construction.</summary>
    public List<CapturedLogEntry> LogEntries { get; } = [];

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        LogEntries.Add(new CapturedLogEntry(logLevel, formatter(state, exception)));
    }

    /// <summary>All formatted messages (convenience accessor).</summary>
    public List<string> Messages => LogEntries.ConvertAll(e => e.Message);
}
