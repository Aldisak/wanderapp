using WanderMeet.Api.Infrastructure.Push;

namespace WanderMeet.Api.IntegrationTests.Infrastructure;

/// <summary>A single captured FCM send invocation.</summary>
public sealed record RecordedFcmSend(string Token, string Title, string Body);

/// <summary>
/// Test double for <see cref="IFcmClient"/> that records every send call and can be configured
/// to throw. Use via <c>App.FcmClient</c> in integration tests.
/// </summary>
public sealed class RecordingFcmClient : IFcmClient
{
    private readonly List<RecordedFcmSend> _sends = [];

    /// <summary>All push sends captured in call order.</summary>
    public IReadOnlyList<RecordedFcmSend> Sends => _sends;

    /// <summary>
    /// If non-null, <see cref="SendAsync"/> throws this exception instead of recording.
    /// Reset to <c>null</c> by <see cref="Reset"/>.
    /// </summary>
    public Exception? ThrowOnSend { get; set; }

    /// <inheritdoc />
    public Task SendAsync(string fcmToken, string title, string body, CancellationToken ct)
    {
        if (ThrowOnSend is not null)
        {
            throw ThrowOnSend;
        }

        _sends.Add(new RecordedFcmSend(fcmToken, title, body));
        return Task.CompletedTask;
    }

    /// <summary>Clears all recorded sends AND resets <see cref="ThrowOnSend"/> to <c>null</c>.</summary>
    public void Reset()
    {
        _sends.Clear();
        ThrowOnSend = null;
    }
}
