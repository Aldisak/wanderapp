using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WanderMeet.Api.Infrastructure.Push;
using Xunit;

namespace WanderMeet.Api.UnitTests.Infrastructure.Push;

/// <summary>
/// Unit tests for <see cref="FirebaseAdminFcmClient"/>.
/// Init is exercised through an injected <see cref="IFirebaseAppInitializer"/> fake
/// so no real <c>FirebaseApp.Create</c> call is made and each test instance owns
/// its own once-flag state.
/// </summary>
public class FirebaseAdminFcmClientTests
{
    private static IOptions<FirebaseOptions> Opts() => Options.Create(new FirebaseOptions
    {
        CredentialsPath = "/fake/path.json",
        ProjectId = "fake-project"
    });

    [Fact]
    public void Constructor_DoesNotInvokeInitializer()
    {
        var initializer = new RecordingInitializer();

        _ = new FirebaseAdminFcmClient(Opts(), initializer, NullLogger<FirebaseAdminFcmClient>.Instance);

        initializer.Calls.Should().Be(0, because: "construction must be side-effect-free");
    }

    [Fact]
    public async Task SendAsync_FirstCallWithBadCredentials_LogsErrorOnceAndDoesNotThrow()
    {
        var initializer = new RecordingInitializer
        {
            Throw = new InvalidOperationException("Simulated bad credentials")
        };
        var capturingLogger = new CapturingLogger<FirebaseAdminFcmClient>();
        var sut = new FirebaseAdminFcmClient(Opts(), initializer, capturingLogger);

        await sut.SendAsync("token", "Title", "Body", TestContext.Current.CancellationToken);

        initializer.Calls.Should().Be(1);
        capturingLogger.LogEntries.Should().Contain(entry =>
            entry.Level == LogLevel.Error &&
            entry.Message.Contains("FCM init failed"),
            because: "first init failure must log Error exactly once");
    }

    [Fact]
    public async Task SendAsync_SubsequentCallAfterInitFailure_LogsWarningAndDoesNotReattemptInit()
    {
        var initializer = new RecordingInitializer
        {
            Throw = new InvalidOperationException("Simulated init failure")
        };
        var capturingLogger = new CapturingLogger<FirebaseAdminFcmClient>();
        var sut = new FirebaseAdminFcmClient(Opts(), initializer, capturingLogger);

        await sut.SendAsync("t1", "T", "B", TestContext.Current.CancellationToken);
        var initCallsAfterFirst = initializer.Calls;

        await sut.SendAsync("t2", "T", "B", TestContext.Current.CancellationToken);

        initializer.Calls.Should().Be(initCallsAfterFirst,
            because: "init must be attempted at most once per process lifetime");

        capturingLogger.LogEntries.Should().Contain(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("FCM message dropped"),
            because: "calls after init failure must log Warning and skip");
    }
}

/// <summary>Records calls to <see cref="IFirebaseAppInitializer.Initialise"/> and optionally throws.</summary>
internal sealed class RecordingInitializer : IFirebaseAppInitializer
{
    public int Calls { get; private set; }
    public Exception? Throw { get; set; }

    public void Initialise(FirebaseOptions options)
    {
        Calls++;
        if (Throw is not null)
        {
            throw Throw;
        }
    }
}
