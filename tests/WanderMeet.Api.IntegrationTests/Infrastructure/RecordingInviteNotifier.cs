using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Invites.Shared;

namespace WanderMeet.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Test double for <see cref="IInviteNotifier"/> that records every invocation and can be
/// configured to throw on any method. Use via <c>App.CreateClientWithInviteNotifier(spy)</c>.
/// Notifier failures must NOT bubble out of the endpoint — set <see cref="ThrowOnSent"/>,
/// <see cref="ThrowOnAccepted"/>, <see cref="ThrowOnDeclined"/>, or <see cref="ThrowOnExpired"/>
/// to verify resilience.
/// </summary>
public sealed class RecordingInviteNotifier : IInviteNotifier
{
    private readonly List<Invite> _sent = [];
    private readonly List<(Invite Invite, Guid MeetupId)> _accepted = [];
    private readonly List<Invite> _declined = [];
    private readonly List<Invite> _expired = [];

    /// <summary>All invites passed to <see cref="InviteSentAsync"/> in call order.</summary>
    public IReadOnlyList<Invite> Sent => _sent;

    /// <summary>All accept events captured by <see cref="InviteAcceptedAsync"/> in call order.</summary>
    public IReadOnlyList<(Invite Invite, Guid MeetupId)> Accepted => _accepted;

    /// <summary>All invites passed to <see cref="InviteDeclinedAsync"/> in call order.</summary>
    public IReadOnlyList<Invite> Declined => _declined;

    /// <summary>All invites passed to <see cref="InviteExpiredAsync"/> in call order.</summary>
    public IReadOnlyList<Invite> Expired => _expired;

    /// <summary>If non-null, <see cref="InviteSentAsync"/> throws this exception instead of recording.</summary>
    public Exception? ThrowOnSent { get; set; }

    /// <summary>If non-null, <see cref="InviteAcceptedAsync"/> throws this exception instead of recording.</summary>
    public Exception? ThrowOnAccepted { get; set; }

    /// <summary>If non-null, <see cref="InviteDeclinedAsync"/> throws this exception instead of recording.</summary>
    public Exception? ThrowOnDeclined { get; set; }

    /// <summary>If non-null, <see cref="InviteExpiredAsync"/> throws this exception instead of recording.</summary>
    public Exception? ThrowOnExpired { get; set; }

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

    /// <inheritdoc />
    public Task InviteDeclinedAsync(Invite invite, CancellationToken ct)
    {
        if (ThrowOnDeclined is not null)
        {
            throw ThrowOnDeclined;
        }

        _declined.Add(invite);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task InviteExpiredAsync(Invite invite, CancellationToken ct)
    {
        if (ThrowOnExpired is not null)
        {
            throw ThrowOnExpired;
        }

        _expired.Add(invite);
        return Task.CompletedTask;
    }
}
