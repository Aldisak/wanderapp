using FakeItEasy;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Invites.Realtime;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.Infrastructure.Push;
using WanderMeet.Api.Infrastructure.SignalR;
using WanderMeet.Shared.Enums;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Invites.Realtime;

/// <summary>Unit tests for <see cref="CompositeInviteNotifier"/> fan-out and error isolation.</summary>
public class CompositeInviteNotifierTests
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
    };

    private static WanderMeetDbContext MakeDbContext()
    {
        var options = new DbContextOptionsBuilder<WanderMeetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WanderMeetDbContext(options);
    }

    private static SignalRInviteNotifier MakeSignalRFake()
    {
        var hubContext = A.Fake<IHubContext<InviteHub>>();
        var dbContext = MakeDbContext();
        var logger = A.Fake<ILogger<SignalRInviteNotifier>>();
        return A.Fake<SignalRInviteNotifier>(opts =>
            opts.WithArgumentsForConstructor([hubContext, dbContext, logger]));
    }

    private static FcmInviteNotifier MakeFcmFake()
    {
        var fcmClient = A.Fake<IFcmClient>();
        var dbContext = MakeDbContext();
        var logger = A.Fake<ILogger<FcmInviteNotifier>>();
        return A.Fake<FcmInviteNotifier>(opts =>
            opts.WithArgumentsForConstructor([fcmClient, dbContext, logger]));
    }

    // -----------------------------------------------------------------------
    // InviteSent fan-out tests
    // -----------------------------------------------------------------------

    /// <summary>When FCM throws, SignalR is still called and the composite does not propagate the exception.</summary>
    [Fact]
    public async Task CompositeInviteNotifier_FcmChildThrows_SignalRChildStillCalled()
    {
        var signalR = MakeSignalRFake();
        var fcm = MakeFcmFake();
        var logger = A.Fake<ILogger<CompositeInviteNotifier>>();
        var composite = new CompositeInviteNotifier(signalR, fcm, logger);
        var invite = MakeInvite();

        A.CallTo(() => fcm.InviteSentAsync(invite, A<CancellationToken>._))
            .Throws(new InvalidOperationException("FCM boom"));

        var act = async () => await composite.InviteSentAsync(invite, CancellationToken.None);

        await act.Should().NotThrowAsync();
        A.CallTo(() => signalR.InviteSentAsync(invite, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    /// <summary>When SignalR throws, FCM is still called and the composite does not propagate the exception.</summary>
    [Fact]
    public async Task CompositeInviteNotifier_SignalRThrows_FcmStillFires()
    {
        var signalR = MakeSignalRFake();
        var fcm = MakeFcmFake();
        var logger = A.Fake<ILogger<CompositeInviteNotifier>>();
        var composite = new CompositeInviteNotifier(signalR, fcm, logger);
        var invite = MakeInvite();

        A.CallTo(() => signalR.InviteSentAsync(invite, A<CancellationToken>._))
            .Throws(new InvalidOperationException("SignalR boom"));

        var act = async () => await composite.InviteSentAsync(invite, CancellationToken.None);

        await act.Should().NotThrowAsync();
        A.CallTo(() => fcm.InviteSentAsync(invite, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    /// <summary>When both children throw, the composite does not propagate either exception.</summary>
    [Fact]
    public async Task CompositeInviteNotifier_BothChildrenThrow_DoesNotPropagate()
    {
        var signalR = MakeSignalRFake();
        var fcm = MakeFcmFake();
        var logger = A.Fake<ILogger<CompositeInviteNotifier>>();
        var composite = new CompositeInviteNotifier(signalR, fcm, logger);
        var invite = MakeInvite();

        A.CallTo(() => signalR.InviteSentAsync(invite, A<CancellationToken>._))
            .Throws(new Exception("SignalR crash"));
        A.CallTo(() => fcm.InviteSentAsync(invite, A<CancellationToken>._))
            .Throws(new Exception("FCM crash"));

        var act = async () => await composite.InviteSentAsync(invite, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // -----------------------------------------------------------------------
    // InviteDeclined: SignalR still called inside try/catch (symmetric design)
    // -----------------------------------------------------------------------

    /// <summary>InviteDeclined routes to SignalR inside its own try/catch even though FCM is a no-op.</summary>
    [Fact]
    public async Task CompositeInviteNotifier_InviteDeclined_SignalRChildStillCalledInTryCatch()
    {
        var signalR = MakeSignalRFake();
        var fcm = MakeFcmFake();
        var logger = A.Fake<ILogger<CompositeInviteNotifier>>();
        var composite = new CompositeInviteNotifier(signalR, fcm, logger);
        var invite = MakeInvite();

        A.CallTo(() => signalR.InviteDeclinedAsync(invite, A<CancellationToken>._))
            .Throws(new Exception("SignalR declined crash"));

        var act = async () => await composite.InviteDeclinedAsync(invite, CancellationToken.None);

        await act.Should().NotThrowAsync();
        A.CallTo(() => signalR.InviteDeclinedAsync(invite, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
