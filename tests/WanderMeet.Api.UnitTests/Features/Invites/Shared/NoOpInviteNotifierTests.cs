using FluentAssertions;
using Microsoft.Extensions.Logging;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Invites.Shared;
using WanderMeet.Shared.Enums;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Invites.Shared;

/// <summary>Unit tests for <see cref="NoOpInviteNotifier"/>.</summary>
public class NoOpInviteNotifierTests
{
    private static Invite MakeInvite() => new()
    {
        Id = Guid.NewGuid(),
        SenderId = Guid.NewGuid(),
        ReceiverId = Guid.NewGuid(),
        HangoutTagId = Guid.NewGuid(),
        PlaceId = Guid.NewGuid(),
        SenderIsThere = false,
        Status = InviteStatus.Pending,
        SentAt = DateTimeOffset.UtcNow,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(48),
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    /// <summary>InviteSentAsync should return a completed task and log at Debug level.</summary>
    [Fact]
    public async Task NoOpInviteNotifier_InviteSentAsync_LogsAtDebugAndReturnsCompletedTask()
    {
        var spyLogger = new SpyLogger<NoOpInviteNotifier>();
        var sut = new NoOpInviteNotifier(spyLogger);
        var invite = MakeInvite();

        var task = sut.InviteSentAsync(invite, TestContext.Current.CancellationToken);
        await task;

        task.IsCompletedSuccessfully.Should().BeTrue();
        spyLogger.DebugCallCount.Should().Be(1);
    }

    /// <summary>InviteAcceptedAsync should return a completed task and log at Debug level.</summary>
    [Fact]
    public async Task NoOpInviteNotifier_InviteAcceptedAsync_LogsAtDebugAndReturnsCompletedTask()
    {
        var spyLogger = new SpyLogger<NoOpInviteNotifier>();
        var sut = new NoOpInviteNotifier(spyLogger);
        var invite = MakeInvite();
        var meetupId = Guid.NewGuid();

        var task = sut.InviteAcceptedAsync(invite, meetupId, TestContext.Current.CancellationToken);
        await task;

        task.IsCompletedSuccessfully.Should().BeTrue();
        spyLogger.DebugCallCount.Should().Be(1);
    }

    /// <summary>Minimal spy logger that counts Debug-level log calls.</summary>
    private sealed class SpyLogger<T> : ILogger<T>
    {
        /// <summary>Number of Debug-level log calls received.</summary>
        public int DebugCallCount { get; private set; }

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Debug)
                DebugCallCount++;
        }
    }
}
