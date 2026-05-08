using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Invites.Shared;

namespace WanderMeet.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Test double for <see cref="IInviteNotifier"/> that records every invocation and can be
/// configured to throw on either method. Use via <c>App.CreateClientWithInviteNotifier(spy)</c>.
/// Notifier failures must NOT bubble out of the endpoint — set <see cref="ThrowOnSent"/>
/// or <see cref="ThrowOnAccepted"/> to verify resilience.
/// </summary>
public sealed class RecordingInviteNotifier : IInviteNotifier
{
    private readonly List<Invite> _sent = [];
    private readonly List<(Invite Invite, Guid MeetupId)> _accepted = [];

    /// <summary>All invites passed to <see cref="InviteSentAsync"/> in call order.</summary>
    public IReadOnlyList<Invite> Sent => _sent;

    /// <summary>All accept events captured by <see cref="InviteAcceptedAsync"/> in call order.</summary>
    public IReadOnlyList<(Invite Invite, Guid MeetupId)> Accepted => _accepted;

    /// <summary>If non-null, <see cref="InviteSentAsync"/> throws this exception instead of recording.</summary>
    public Exception? ThrowOnSent { get; set; }

    /// <summary>If non-null, <see cref="InviteAcceptedAsync"/> throws this exception instead of recording.</summary>
    public Exception? ThrowOnAccepted { get; set; }

    /// <inheritdoc />
    public Task InviteSentAsync(Invite invite, CancellationToken ct)
    {
        if (ThrowOnSent is not null)
        {
            throw ThrowOnSent;
        }

        _sent.Add(invite);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task InviteAcceptedAsync(Invite invite, Guid meetupId, CancellationToken ct)
    {
        if (ThrowOnAccepted is not null)
        {
            throw ThrowOnAccepted;
        }

        _accepted.Add((invite, meetupId));
        return Task.CompletedTask;
    }
}
