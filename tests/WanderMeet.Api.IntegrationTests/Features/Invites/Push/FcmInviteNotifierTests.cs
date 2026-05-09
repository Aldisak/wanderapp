using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Invites.Realtime.Shared;
using WanderMeet.Api.Features.Invites.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared;
using WanderMeet.Shared.Enums;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Invites.Push;

/// <summary>Integration tests for FCM push via <see cref="RecordingFcmClient"/>.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class FcmInviteNotifierTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private const double CityLon = 14.42;
    private const double CityLat = 50.08;

    private static Point CityCenter() => new(CityLon, CityLat) { SRID = 4326 };

    // -----------------------------------------------------------------------
    // Seed helpers
    // -----------------------------------------------------------------------

    private static User MakeUser(string sub, Guid cityId, DateTimeOffset now, string? fcmToken = null) => new()
    {
        Id = Guid.NewGuid(),
        AzureAdB2CId = sub,
        FirstName = sub.Length > 8 ? sub[..8] : sub,
        CityId = cityId,
        CreatedAt = now,
        LastActiveAt = now,
        FcmToken = fcmToken,
    };

    private static HangoutTag MakeHangoutTag(HangoutTagSlug slug, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        Slug = slug,
        Label = slug.ToString(),
        Emoji = "☕",
        CreatedAt = now,
    };

    private static Place MakePlace(Guid cityId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        GooglePlaceId = $"gp_{Guid.NewGuid()}",
        Name = "Test Place",
        CityId = cityId,
        Location = CityCenter(),
        Category = PlaceCategory.Cafe,
        CreatedAt = now,
    };

    private async Task<(Guid senderId, Guid receiverId, HangoutTag tag, Place place)> SeedAsync(
        string senderSub,
        string receiverSub,
        DateTimeOffset now,
        string? receiverFcmToken = null,
        HangoutTagSlug slug = HangoutTagSlug.Coffee)
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

        var tag = MakeHangoutTag(slug, now);
        db.HangoutTags.Add(tag);

        var place = MakePlace(city.Id, now);
        db.Places.Add(place);

        var sender = MakeUser(senderSub, city.Id, now);
        var receiver = MakeUser(receiverSub, city.Id, now, receiverFcmToken);
        db.Users.Add(sender);
        db.Users.Add(receiver);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (sender.Id, receiver.Id, tag, place);
    }

    // -----------------------------------------------------------------------
    // InviteSent — standard push (receiver has token)
    // -----------------------------------------------------------------------

    /// <summary>Standard invite push: receiver has FCM token → recording client captures the send with correct title.</summary>
    [Fact]
    public async Task FcmInviteNotifier_InviteSent_HappyPath_FiresReceiverPushWithStandardTitle()
    {
        const string SENDER_SUB = "fcm-sent-sender-01";
        const string RECEIVER_SUB = "fcm-sent-rcvr-01";
        const string RECEIVER_TOKEN = "fcm-token-receiver-sent-happy";
        var now = App.FakeTimeProvider.GetUtcNow();

        var (_, receiverId, tag, place) = await SeedAsync(SENDER_SUB, RECEIVER_SUB, now, RECEIVER_TOKEN, HangoutTagSlug.Coffee);

        var client = App.CreateAuthenticatedClient(SENDER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.100.1.1");

        var response = await client.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = receiverId, HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        App.FcmClient.Sends.Should().HaveCount(1);
        var send = App.FcmClient.Sends[0];
        send.Token.Should().Be(RECEIVER_TOKEN);
        send.Title.Should().Be("Coffee at Test Place?");
        send.Body.Should().Be($"{SENDER_SUB[..8]} wants to meet you at Test Place.");
    }

    /// <summary>SenderIsThere = true → push uses ImThere template with ☕ in title.</summary>
    [Fact]
    public async Task FcmInviteNotifier_InviteSent_SenderIsThereTrue_FiresImTherePushTitle()
    {
        const string SENDER_SUB = "fcm-sent-sender-02";
        const string RECEIVER_SUB = "fcm-sent-rcvr-02";
        const string RECEIVER_TOKEN = "fcm-token-receiver-imthere";
        var now = App.FakeTimeProvider.GetUtcNow();

        var (_, receiverId, tag, place) = await SeedAsync(SENDER_SUB, RECEIVER_SUB, now, RECEIVER_TOKEN, HangoutTagSlug.Coffee);

        var client = App.CreateAuthenticatedClient(SENDER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.100.1.2");

        var response = await client.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = receiverId, HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = true },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        App.FcmClient.Sends.Should().HaveCount(1);
        var send = App.FcmClient.Sends[0];
        send.Title.Should().Contain("☕");
        send.Title.Should().Be($"{SENDER_SUB[..8]} is at Test Place ☕");
    }

    /// <summary>Receiver has null FCM token → no FCM send recorded.</summary>
    [Fact]
    public async Task FcmInviteNotifier_InviteSent_ReceiverFcmTokenNull_SilentlySkipsPush()
    {
        const string SENDER_SUB = "fcm-sent-sender-03";
        const string RECEIVER_SUB = "fcm-sent-rcvr-03";
        var now = App.FakeTimeProvider.GetUtcNow();

        var (_, receiverId, tag, place) = await SeedAsync(SENDER_SUB, RECEIVER_SUB, now, receiverFcmToken: null);

        var client = App.CreateAuthenticatedClient(SENDER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.100.1.3");

        var response = await client.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = receiverId, HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        App.FcmClient.Sends.Should().BeEmpty();
    }

    /// <summary>Receiver has empty FCM token → no FCM send recorded.</summary>
    [Fact]
    public async Task FcmInviteNotifier_InviteSent_ReceiverFcmTokenEmpty_SilentlySkipsPush()
    {
        const string SENDER_SUB = "fcm-sent-sender-04";
        const string RECEIVER_SUB = "fcm-sent-rcvr-04";
        var now = App.FakeTimeProvider.GetUtcNow();

        var (_, receiverId, tag, place) = await SeedAsync(SENDER_SUB, RECEIVER_SUB, now, receiverFcmToken: "");

        var client = App.CreateAuthenticatedClient(SENDER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.100.1.4");

        var response = await client.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = receiverId, HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        App.FcmClient.Sends.Should().BeEmpty();
    }

    /// <summary>Receiver is soft-deleted → no FCM send recorded, endpoint returns 400 (receiver not found).</summary>
    [Fact]
    public async Task FcmInviteNotifier_InviteSent_ReceiverSoftDeleted_SilentlySkipsPush()
    {
        const string SENDER_SUB = "fcm-sent-sender-05";
        const string RECEIVER_SUB = "fcm-sent-rcvr-05";
        var now = App.FakeTimeProvider.GetUtcNow();

        var (senderId, receiverId, tag, place) = await SeedAsync(SENDER_SUB, RECEIVER_SUB, now, "fcm-token-softdeleted");

        // Soft-delete the receiver directly
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var receiver = await db.Users.FindAsync([receiverId], TestContext.Current.CancellationToken);
            receiver!.DeletedAt = now;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SENDER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.100.1.5");

        // Endpoint guards: receiver not found (DeletedAt != null) → 400 before notifier fires
        var response = await client.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = receiverId, HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = false },
            TestContext.Current.CancellationToken);

        // Receiver soft-delete is caught at endpoint level (receiver is filtered out)
        // so the endpoint returns 400 and no FCM send should happen
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        App.FcmClient.Sends.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // InviteAccepted — push to sender
    // -----------------------------------------------------------------------

    /// <summary>Receiver accepts → sender (with FCM token) gets push with correct accepted template.</summary>
    [Fact]
    public async Task FcmInviteNotifier_InviteAccepted_HappyPath_FiresSenderPushWithAcceptedTitle()
    {
        const string SENDER_SUB = "fcm-accepted-sender-01";
        const string RECEIVER_SUB = "fcm-accepted-rcvr-01";
        const string SENDER_TOKEN = "fcm-token-sender-accepted-happy";
        var now = App.FakeTimeProvider.GetUtcNow();

        // Seed sender WITH token; receiver without
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var city = new City { Id = Guid.NewGuid(), Name = "City2", Country = "CZ", Location = CityCenter(), CreatedAt = now };
            db.Cities.Add(city);
            var tag = MakeHangoutTag(HangoutTagSlug.Coffee, now);
            db.HangoutTags.Add(tag);
            var place = MakePlace(city.Id, now);
            db.Places.Add(place);

            var sender = MakeUser(SENDER_SUB, city.Id, now, SENDER_TOKEN);
            var receiver = MakeUser(RECEIVER_SUB, city.Id, now, null);
            db.Users.Add(sender);
            db.Users.Add(receiver);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var invite = new Invite
            {
                Id = Guid.NewGuid(),
                SenderId = sender.Id,
                ReceiverId = receiver.Id,
                HangoutTagId = tag.Id,
                PlaceId = place.Id,
                SenderIsThere = false,
                Status = InviteStatus.Pending,
                SentAt = now,
                ExpiresAt = now + ValidationConstants.InviteExpiryWindow,
                CreatedAt = now,
            };
            db.Invites.Add(invite);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Reset sends from any seeding side-effects (if any)
            App.FcmClient.Reset();

            var receiverClient = App.CreateAuthenticatedClient(RECEIVER_SUB);
            receiverClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.100.2.1");
            var acceptResponse = await receiverClient.PatchAsJsonAsync(
                $"api/v1/invites/{invite.Id}/accept",
                new { },
                TestContext.Current.CancellationToken);
            acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        App.FcmClient.Sends.Should().HaveCount(1);
        var send = App.FcmClient.Sends[0];
        send.Token.Should().Be(SENDER_TOKEN);
        send.Title.Should().Be("See you there!");
        send.Body.Should().Contain("accepted");
        send.Body.Should().Contain("Test Place");
    }

    /// <summary>Sender has no FCM token → no push on accept.</summary>
    [Fact]
    public async Task FcmInviteNotifier_InviteAccepted_SenderFcmTokenNull_SilentlySkipsPush()
    {
        const string SENDER_SUB = "fcm-accepted-sender-02";
        const string RECEIVER_SUB = "fcm-accepted-rcvr-02";
        var now = App.FakeTimeProvider.GetUtcNow();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var city = new City { Id = Guid.NewGuid(), Name = "City3", Country = "CZ", Location = CityCenter(), CreatedAt = now };
            db.Cities.Add(city);
            var tag = MakeHangoutTag(HangoutTagSlug.Coffee, now);
            db.HangoutTags.Add(tag);
            var place = MakePlace(city.Id, now);
            db.Places.Add(place);
            // sender has NO token
            var sender = MakeUser(SENDER_SUB, city.Id, now, null);
            var receiver = MakeUser(RECEIVER_SUB, city.Id, now, null);
            db.Users.Add(sender);
            db.Users.Add(receiver);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var invite = new Invite
            {
                Id = Guid.NewGuid(),
                SenderId = sender.Id,
                ReceiverId = receiver.Id,
                HangoutTagId = tag.Id,
                PlaceId = place.Id,
                SenderIsThere = false,
                Status = InviteStatus.Pending,
                SentAt = now,
                ExpiresAt = now + ValidationConstants.InviteExpiryWindow,
                CreatedAt = now,
            };
            db.Invites.Add(invite);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            App.FcmClient.Reset();
            var receiverClient = App.CreateAuthenticatedClient(RECEIVER_SUB);
            receiverClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.100.2.2");
            await receiverClient.PatchAsJsonAsync($"api/v1/invites/{invite.Id}/accept", new { }, TestContext.Current.CancellationToken);
        }

        App.FcmClient.Sends.Should().BeEmpty();
    }

    /// <summary>Receiver is soft-deleted after invite was created → accepted push to sender is suppressed (PII guard).</summary>
    [Fact]
    public async Task FcmInviteNotifier_InviteAccepted_ReceiverSoftDeleted_SilentlySkipsPush()
    {
        // Note: the endpoint itself prevents acceptance if receiver is deleted (receiver must exist to accept),
        // so this scenario is primarily a notifier-level guard test.
        // We seed the invite directly and soft-delete receiver, then call the accept endpoint via the receiver's sub
        // which will fail because the receiver is deleted — so no FCM push should happen.
        const string SENDER_SUB = "fcm-accepted-sender-03";
        const string RECEIVER_SUB = "fcm-accepted-rcvr-03";
        const string SENDER_TOKEN = "fcm-token-sender-accepted-rcvdel";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid inviteId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var city = new City { Id = Guid.NewGuid(), Name = "City4", Country = "CZ", Location = CityCenter(), CreatedAt = now };
            db.Cities.Add(city);
            var tag = MakeHangoutTag(HangoutTagSlug.Coffee, now);
            db.HangoutTags.Add(tag);
            var place = MakePlace(city.Id, now);
            db.Places.Add(place);
            var sender = MakeUser(SENDER_SUB, city.Id, now, SENDER_TOKEN);
            var receiver = MakeUser(RECEIVER_SUB, city.Id, now, null);
            db.Users.Add(sender);
            db.Users.Add(receiver);
            var invite = new Invite
            {
                Id = Guid.NewGuid(),
                SenderId = sender.Id,
                ReceiverId = receiver.Id,
                HangoutTagId = tag.Id,
                PlaceId = place.Id,
                SenderIsThere = false,
                Status = InviteStatus.Pending,
                SentAt = now,
                ExpiresAt = now + ValidationConstants.InviteExpiryWindow,
                CreatedAt = now,
            };
            db.Invites.Add(invite);
            inviteId = invite.Id;
            // Soft-delete the receiver
            receiver.DeletedAt = now;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        App.FcmClient.Reset();
        var receiverClient = App.CreateAuthenticatedClient(RECEIVER_SUB);
        receiverClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.100.2.3");
        var acceptResponse = await receiverClient.PatchAsJsonAsync(
            $"api/v1/invites/{inviteId}/accept",
            new { },
            TestContext.Current.CancellationToken);

        // Receiver is deleted → endpoint returns 404 (no user profile)
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        App.FcmClient.Sends.Should().BeEmpty();
    }

    /// <summary>Sender is soft-deleted → accepted push is suppressed (skip if sender deleted).</summary>
    [Fact]
    public async Task FcmInviteNotifier_InviteAccepted_SenderSoftDeleted_SilentlySkipsPush()
    {
        const string SENDER_SUB = "fcm-accepted-sender-04";
        const string RECEIVER_SUB = "fcm-accepted-rcvr-04";
        const string SENDER_TOKEN = "fcm-token-sender-accepted-snddel";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid inviteId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var city = new City { Id = Guid.NewGuid(), Name = "City5", Country = "CZ", Location = CityCenter(), CreatedAt = now };
            db.Cities.Add(city);
            var tag = MakeHangoutTag(HangoutTagSlug.Coffee, now);
            db.HangoutTags.Add(tag);
            var place = MakePlace(city.Id, now);
            db.Places.Add(place);
            var sender = MakeUser(SENDER_SUB, city.Id, now, SENDER_TOKEN);
            var receiver = MakeUser(RECEIVER_SUB, city.Id, now, null);
            db.Users.Add(sender);
            db.Users.Add(receiver);
            var invite = new Invite
            {
                Id = Guid.NewGuid(),
                SenderId = sender.Id,
                ReceiverId = receiver.Id,
                HangoutTagId = tag.Id,
                PlaceId = place.Id,
                SenderIsThere = false,
                Status = InviteStatus.Pending,
                SentAt = now,
                ExpiresAt = now + ValidationConstants.InviteExpiryWindow,
                CreatedAt = now,
            };
            db.Invites.Add(invite);
            inviteId = invite.Id;
            // Soft-delete the sender
            sender.DeletedAt = now;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        App.FcmClient.Reset();
        var receiverClient = App.CreateAuthenticatedClient(RECEIVER_SUB);
        receiverClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.100.2.4");
        var acceptResponse = await receiverClient.PatchAsJsonAsync(
            $"api/v1/invites/{inviteId}/accept",
            new { },
            TestContext.Current.CancellationToken);

        // Receiver can still accept (they're not deleted) → 200, but FCM skips (sender deleted)
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        App.FcmClient.Sends.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // InviteDeclined / InviteExpired — no FCM push
    // -----------------------------------------------------------------------

    /// <summary>Receiver declines → no FCM push (FCM is no-op for declined).</summary>
    [Fact]
    public async Task FcmInviteNotifier_InviteDeclined_FiresNoFcmPush()
    {
        const string SENDER_SUB = "fcm-declined-sender-01";
        const string RECEIVER_SUB = "fcm-declined-rcvr-01";
        const string SENDER_TOKEN = "fcm-token-sender-declined";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid inviteId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var city = new City { Id = Guid.NewGuid(), Name = "City6", Country = "CZ", Location = CityCenter(), CreatedAt = now };
            db.Cities.Add(city);
            var tag = MakeHangoutTag(HangoutTagSlug.Coffee, now);
            db.HangoutTags.Add(tag);
            var place = MakePlace(city.Id, now);
            db.Places.Add(place);
            var sender = MakeUser(SENDER_SUB, city.Id, now, SENDER_TOKEN);
            var receiver = MakeUser(RECEIVER_SUB, city.Id, now, null);
            db.Users.Add(sender);
            db.Users.Add(receiver);
            var invite = new Invite
            {
                Id = Guid.NewGuid(),
                SenderId = sender.Id,
                ReceiverId = receiver.Id,
                HangoutTagId = tag.Id,
                PlaceId = place.Id,
                SenderIsThere = false,
                Status = InviteStatus.Pending,
                SentAt = now,
                ExpiresAt = now + ValidationConstants.InviteExpiryWindow,
                CreatedAt = now,
            };
            db.Invites.Add(invite);
            inviteId = invite.Id;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        App.FcmClient.Reset();
        var receiverClient = App.CreateAuthenticatedClient(RECEIVER_SUB);
        receiverClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.100.3.1");
        var declineResponse = await receiverClient.PatchAsJsonAsync(
            $"api/v1/invites/{inviteId}/decline",
            new { },
            TestContext.Current.CancellationToken);

        declineResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        App.FcmClient.Sends.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // CompositeInviteNotifier — FCM throws, SignalR still fires, endpoint returns 201
    // -----------------------------------------------------------------------

    /// <summary>
    /// CompositeInviteNotifier resilience: FCM throws → endpoint still returns 201
    /// and SignalR push still fires to the receiver.
    /// </summary>
    [Fact]
    public async Task CompositeInviteNotifier_FcmThrows_SignalRStillFiresAndEndpointReturns201()
    {
        const string SENDER_SUB = "fcm-composite-sender-01";
        const string RECEIVER_SUB = "fcm-composite-rcvr-01";
        const string RECEIVER_TOKEN = "fcm-token-composite-receiver";
        var now = App.FakeTimeProvider.GetUtcNow();

        var (_, receiverId, tag, place) = await SeedAsync(SENDER_SUB, RECEIVER_SUB, now, RECEIVER_TOKEN);

        // Inject FCM failure
        App.FcmClient.ThrowOnSend = new InvalidOperationException("FCM boom in composite test");

        // Set up a SignalR listener on the receiver side to confirm SignalR still fires
        var receiverConn = App.CreateAuthenticatedSignalRConnection(RECEIVER_SUB);
        var receiverTcs = new TaskCompletionSource<InviteHubReceivedDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        receiverConn.On<InviteHubReceivedDto>("InviteReceived", dto => receiverTcs.TrySetResult(dto));
        await receiverConn.StartAsync(TestContext.Current.CancellationToken);

        var senderClient = App.CreateAuthenticatedClient(SENDER_SUB);
        senderClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.100.4.1");

        var response = await senderClient.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = receiverId, HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // SignalR should still have fired
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var receivedDto = await receiverTcs.Task.WaitAsync(cts.Token);
        receivedDto.Should().NotBeNull();

        await receiverConn.StopAsync(TestContext.Current.CancellationToken);
        await receiverConn.DisposeAsync();
    }
}
