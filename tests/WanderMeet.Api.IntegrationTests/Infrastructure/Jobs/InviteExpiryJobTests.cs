using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.Infrastructure.Jobs;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared;
using WanderMeet.Shared.Enums;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Infrastructure.Jobs;

/// <summary>Integration tests for <see cref="InviteExpiryJob"/>.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class InviteExpiryJobTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private static Point CityCenter() => new(14.42, 50.08) { SRID = 4326 };

    private async Task<(Guid cityId, Guid placeId, Guid hangoutTagId)> SeedMinimalAsync(DateTimeOffset now)
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();

        var city = new City
        {
            Id = Guid.NewGuid(),
            Name = "Test City",
            Country = "CZ",
            Location = CityCenter(),
            CreatedAt = now,
        };
        db.Cities.Add(city);

        var tag = new HangoutTag
        {
            Id = Guid.NewGuid(),
            Slug = HangoutTagSlug.Coffee,
            Label = "Coffee",
            Emoji = "☕",
            CreatedAt = now,
        };
        db.HangoutTags.Add(tag);

        var place = new Place
        {
            Id = Guid.NewGuid(),
            GooglePlaceId = $"gp_{Guid.NewGuid()}",
            Name = "Test Place",
            CityId = city.Id,
            Location = CityCenter(),
            Category = PlaceCategory.Cafe,
            CreatedAt = now,
        };
        db.Places.Add(place);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (city.Id, place.Id, tag.Id);
    }

    private static User MakeUser(string sub, Guid cityId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        AzureAdB2CId = sub,
        FirstName = sub.Length > 8 ? sub[..8] : sub,
        CityId = cityId,
        CreatedAt = now,
        LastActiveAt = now,
    };

    private static Invite MakeInvite(Guid senderId, Guid receiverId, Guid tagId, Guid placeId,
        DateTimeOffset sentAt, DateTimeOffset expiresAt, InviteStatus status = InviteStatus.Pending) => new()
    {
        Id = Guid.NewGuid(),
        SenderId = senderId,
        ReceiverId = receiverId,
        HangoutTagId = tagId,
        PlaceId = placeId,
        Status = status,
        SentAt = sentAt,
        ExpiresAt = expiresAt,
        CreatedAt = sentAt,
    };

    /// <summary>Pending invites past their ExpiresAt are flipped to Expired and the notifier fires per invite.</summary>
    [Fact]
    public async Task InviteExpiryJob_ExecuteAsync_PendingInvitesPastExpiry_AreFlippedToExpiredAndNotifierFires()
    {
        var now = App.FakeTimeProvider.GetUtcNow();
        var (cityId, placeId, tagId) = await SeedMinimalAsync(now);
        var ct = TestContext.Current.CancellationToken;

        Guid expiredInvite1Id, expiredInvite2Id, pendingInviteId, acceptedInviteId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();

            var sender = MakeUser("expiry-sender-01", cityId, now);
            var receiver1 = MakeUser("expiry-rcvr-01", cityId, now);
            var receiver2 = MakeUser("expiry-rcvr-02", cityId, now);
            var receiver3 = MakeUser("expiry-rcvr-03", cityId, now);
            db.Users.AddRange(sender, receiver1, receiver2, receiver3);
            await db.SaveChangesAsync(ct);

            // 2 invites past expiry (should be expired)
            var invite1 = MakeInvite(sender.Id, receiver1.Id, tagId, placeId,
                sentAt: now - TimeSpan.FromHours(50),
                expiresAt: now - TimeSpan.FromHours(2));
            var invite2 = MakeInvite(sender.Id, receiver2.Id, tagId, placeId,
                sentAt: now - TimeSpan.FromHours(60),
                expiresAt: now - TimeSpan.FromMinutes(1));

            // 1 invite still within window (not yet expired)
            var invite3 = MakeInvite(sender.Id, receiver3.Id, tagId, placeId,
                sentAt: now - TimeSpan.FromHours(10),
                expiresAt: now + TimeSpan.FromHours(38));

            // 1 already-accepted invite (should not be touched)
            var invite4 = MakeInvite(sender.Id, receiver3.Id, tagId, placeId,
                sentAt: now - TimeSpan.FromHours(72),
                expiresAt: now - TimeSpan.FromHours(24),
                status: InviteStatus.Accepted);
            invite4.RespondedAt = now - TimeSpan.FromHours(70);

            db.Invites.AddRange(invite1, invite2, invite3, invite4);
            await db.SaveChangesAsync(ct);

            expiredInvite1Id = invite1.Id;
            expiredInvite2Id = invite2.Id;
            pendingInviteId = invite3.Id;
            acceptedInviteId = invite4.Id;
        }

        // Use the RecordingInviteNotifier from the fixture (already registered)
        // We'll use a separate RecordingInviteNotifier instance injected via a scope override
        var notifierSpy = new RecordingInviteNotifier();

        // Resolve the job with the spy notifier by creating a derived factory scope
        using var jobScope = App.Services.CreateScope();
        // Override IInviteNotifier in this scope by creating a sub-scope manually
        var notifier = (WanderMeet.Api.Features.Invites.Shared.IInviteNotifier)notifierSpy;
        var jobServices = new ServiceCollection();
        var dbCtx = jobScope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var timeProvider = jobScope.ServiceProvider.GetRequiredService<TimeProvider>();
        var logger = jobScope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<InviteExpiryJob>>();

        var job = new InviteExpiryJob(dbCtx, notifier, timeProvider, logger);
        await job.ExecuteAsync(ct);

        // Assert: 2 invites now Expired
        using var assertScope = App.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var invites = await assertDb.Invites.AsNoTracking()
            .Where(i => new[] { expiredInvite1Id, expiredInvite2Id, pendingInviteId, acceptedInviteId }.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, ct);

        invites[expiredInvite1Id].Status.Should().Be(InviteStatus.Expired);
        invites[expiredInvite1Id].RespondedAt.Should().Be(now);
        invites[expiredInvite2Id].Status.Should().Be(InviteStatus.Expired);
        invites[expiredInvite2Id].RespondedAt.Should().Be(now);
        invites[pendingInviteId].Status.Should().Be(InviteStatus.Pending, "invite not yet expired");
        invites[acceptedInviteId].Status.Should().Be(InviteStatus.Accepted, "accepted invite untouched");

        // Notifier fired exactly for the 2 expired invites
        notifierSpy.Expired.Should().HaveCount(2);
        notifierSpy.Expired.Select(i => i.Id).Should().BeEquivalentTo([expiredInvite1Id, expiredInvite2Id]);
    }

    /// <summary>Pending invites not yet expired are left alone.</summary>
    [Fact]
    public async Task InviteExpiryJob_ExecuteAsync_PendingInvitesNotYetExpired_LeftAlone()
    {
        var now = App.FakeTimeProvider.GetUtcNow();
        var (cityId, placeId, tagId) = await SeedMinimalAsync(now);
        var ct = TestContext.Current.CancellationToken;

        Guid pendingInviteId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var sender = MakeUser("notyet-sender-01", cityId, now);
            var receiver = MakeUser("notyet-rcvr-01", cityId, now);
            db.Users.AddRange(sender, receiver);
            await db.SaveChangesAsync(ct);

            var invite = MakeInvite(sender.Id, receiver.Id, tagId, placeId,
                sentAt: now - TimeSpan.FromHours(1),
                expiresAt: now + TimeSpan.FromHours(47));
            db.Invites.Add(invite);
            await db.SaveChangesAsync(ct);
            pendingInviteId = invite.Id;
        }

        var notifierSpy = new RecordingInviteNotifier();
        using var jobScope = App.Services.CreateScope();
        var job = new InviteExpiryJob(
            jobScope.ServiceProvider.GetRequiredService<WanderMeetDbContext>(),
            notifierSpy,
            jobScope.ServiceProvider.GetRequiredService<TimeProvider>(),
            jobScope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<InviteExpiryJob>>());
        await job.ExecuteAsync(ct);

        using var assertScope = App.Services.CreateScope();
        var db2 = assertScope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var invite2 = await db2.Invites.AsNoTracking().FirstAsync(i => i.Id == pendingInviteId, ct);
        invite2.Status.Should().Be(InviteStatus.Pending);
        notifierSpy.Expired.Should().BeEmpty();
    }

    /// <summary>Notifier throws → persisted Expired status is NOT rolled back.</summary>
    [Fact]
    public async Task InviteExpiryJob_ExecuteAsync_NotifierThrows_PersistedStateUnchanged()
    {
        var now = App.FakeTimeProvider.GetUtcNow();
        var (cityId, placeId, tagId) = await SeedMinimalAsync(now);
        var ct = TestContext.Current.CancellationToken;

        Guid inviteId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var sender = MakeUser("notifier-throw-sender-01", cityId, now);
            var receiver = MakeUser("notifier-throw-rcvr-01", cityId, now);
            db.Users.AddRange(sender, receiver);
            await db.SaveChangesAsync(ct);

            var invite = MakeInvite(sender.Id, receiver.Id, tagId, placeId,
                sentAt: now - TimeSpan.FromHours(50),
                expiresAt: now - TimeSpan.FromHours(2));
            db.Invites.Add(invite);
            await db.SaveChangesAsync(ct);
            inviteId = invite.Id;
        }

        var notifierSpy = new RecordingInviteNotifier
        {
            ThrowOnExpired = new InvalidOperationException("Notifier failure test")
        };

        using var jobScope = App.Services.CreateScope();
        var job = new InviteExpiryJob(
            jobScope.ServiceProvider.GetRequiredService<WanderMeetDbContext>(),
            notifierSpy,
            jobScope.ServiceProvider.GetRequiredService<TimeProvider>(),
            jobScope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<InviteExpiryJob>>());

        // Job must NOT throw even when notifier throws
        await job.ExecuteAsync(ct);

        // DB state is Expired (persisted before notifier was called)
        using var assertScope = App.Services.CreateScope();
        var db2 = assertScope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var invite2 = await db2.Invites.AsNoTracking().FirstAsync(i => i.Id == inviteId, ct);
        invite2.Status.Should().Be(InviteStatus.Expired, "DB persist happened before notifier — must not roll back");
        invite2.RespondedAt.Should().Be(now);
    }
}
