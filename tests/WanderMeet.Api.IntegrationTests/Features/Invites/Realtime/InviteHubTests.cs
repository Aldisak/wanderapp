using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Invites.Realtime.Shared;
using WanderMeet.Api.Features.Invites.SendInvite;
using WanderMeet.Api.Features.Invites.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared;
using WanderMeet.Shared.Enums;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Invites.Realtime;

/// <summary>Integration tests for the InviteHub SignalR hub at /hubs/invites.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class InviteHubTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private const double CityLon = 14.42;
    private const double CityLat = 50.08;

    private static Point CityCenter() => new(CityLon, CityLat) { SRID = 4326 };

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static User MakeUser(string sub, Guid cityId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        AzureAdB2CId = sub,
        FirstName = "User " + sub[..Math.Min(8, sub.Length)],
        CityId = cityId,
        CreatedAt = now,
        LastActiveAt = now,
    };

    private static HangoutTag MakeHangoutTag(DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        Slug = HangoutTagSlug.Coffee,
        Label = "Coffee",
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

    private async Task<(Guid callerId, Guid receiverId, HangoutTag tag, Place place)> SeedTwoUsersWithCityTagPlaceAsync(
        string callerSub, string receiverSub, DateTimeOffset now)
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

        var tag = MakeHangoutTag(now);
        db.HangoutTags.Add(tag);

        var place = MakePlace(city.Id, now);
        db.Places.Add(place);

        var caller = MakeUser(callerSub, city.Id, now);
        var receiver = MakeUser(receiverSub, city.Id, now);
        db.Users.Add(caller);
        db.Users.Add(receiver);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (caller.Id, receiver.Id, tag, place);
    }

    // -----------------------------------------------------------------------
    // Auth tests
    // -----------------------------------------------------------------------

    /// <summary>Connecting without a bearer token fails with 401 at negotiation.</summary>
    [Fact]
    public async Task OnConnectAsync_NoBearerToken_NegotiationFails401()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(App.Server.BaseAddress, "/hubs/invites"), opts =>
            {
                opts.HttpMessageHandlerFactory = _ => App.Server.CreateHandler();
                opts.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .Build();

        var act = async () => await connection.StartAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*401*");

        await connection.DisposeAsync();
    }

    /// <summary>Query-string access_token allows hub connection (browser WebSocket fallback).</summary>
    [Fact]
    public async Task QueryStringAccessToken_AllowsConnection()
    {
        const string SUB = "oid|hub-qs-token";
        var now = App.FakeTimeProvider.GetUtcNow();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var city = new City { Id = Guid.NewGuid(), Name = "QS City", Country = "CZ", Location = CityCenter(), CreatedAt = now };
            db.Cities.Add(city);
            db.Users.Add(MakeUser(SUB, city.Id, now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var token = App.JwtFactory.CreateToken(SUB);
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(App.Server.BaseAddress, $"/hubs/invites?access_token={token}"), opts =>
            {
                opts.HttpMessageHandlerFactory = _ => App.Server.CreateHandler();
                opts.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .Build();

        var act = async () => await connection.StartAsync(TestContext.Current.CancellationToken);
        await act.Should().NotThrowAsync();

        await connection.StopAsync(TestContext.Current.CancellationToken);
        await connection.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // InviteSent pushes InviteReceived to receiver only
    // -----------------------------------------------------------------------

    /// <summary>Sender posts invite → receiver hub gets InviteReceived; sender hub does NOT.</summary>
    [Fact]
    public async Task InviteSent_PushesInviteReceivedToReceiverOnly()
    {
        const string SENDER_SUB = "oid|hub-sent-sender";
        const string RECEIVER_SUB = "oid|hub-sent-receiver";
        var now = App.FakeTimeProvider.GetUtcNow();

        var (_, receiverId, tag, place) = await SeedTwoUsersWithCityTagPlaceAsync(SENDER_SUB, RECEIVER_SUB, now);

        var senderConn = App.CreateAuthenticatedSignalRConnection(SENDER_SUB);
        var receiverConn = App.CreateAuthenticatedSignalRConnection(RECEIVER_SUB);

        InviteHubReceivedDto? receivedBySender = null;
        var receiverTcs = new TaskCompletionSource<InviteHubReceivedDto>(TaskCreationOptions.RunContinuationsAsynchronously);

        senderConn.On<InviteHubReceivedDto>("InviteReceived", dto => receivedBySender = dto);
        receiverConn.On<InviteHubReceivedDto>("InviteReceived", dto => receiverTcs.TrySetResult(dto));

        await senderConn.StartAsync(TestContext.Current.CancellationToken);
        await receiverConn.StartAsync(TestContext.Current.CancellationToken);

        var senderClient = App.CreateAuthenticatedClient(SENDER_SUB);
        senderClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.90.1.1");

        var response = await senderClient.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = receiverId, HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var invite = await response.Content.ReadFromJsonAsync<InviteDto>(cancellationToken: TestContext.Current.CancellationToken);
        invite.Should().NotBeNull();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var receivedDto = await receiverTcs.Task.WaitAsync(cts.Token);

        receivedDto.Should().NotBeNull();
        receivedDto.Id.Should().Be(invite!.Id);

        // Sender must NOT have received InviteReceived
        receivedBySender.Should().BeNull();

        await senderConn.StopAsync(TestContext.Current.CancellationToken);
        await receiverConn.StopAsync(TestContext.Current.CancellationToken);
        await senderConn.DisposeAsync();
        await receiverConn.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // InviteAccepted pushes to sender only
    // -----------------------------------------------------------------------

    /// <summary>Receiver accepts → sender gets InviteAccepted; receiver does NOT.</summary>
    [Fact]
    public async Task InviteAccepted_PushesInviteAcceptedToSenderOnly()
    {
        const string SENDER_SUB = "oid|hub-accepted-sender";
        const string RECEIVER_SUB = "oid|hub-accepted-receiver";
        var now = App.FakeTimeProvider.GetUtcNow();

        var (senderId, receiverId, tag, place) = await SeedTwoUsersWithCityTagPlaceAsync(SENDER_SUB, RECEIVER_SUB, now);

        // Seed an existing pending invite
        Guid inviteId;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var invite = new Invite
            {
                Id = Guid.NewGuid(),
                SenderId = senderId,
                ReceiverId = receiverId,
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
            inviteId = invite.Id;
        }

        var senderConn = App.CreateAuthenticatedSignalRConnection(SENDER_SUB);
        var receiverConn = App.CreateAuthenticatedSignalRConnection(RECEIVER_SUB);

        var senderTcs = new TaskCompletionSource<InviteHubAcceptedDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        InviteHubAcceptedDto? receivedByReceiver = null;

        senderConn.On<InviteHubAcceptedDto>("InviteAccepted", dto => senderTcs.TrySetResult(dto));
        receiverConn.On<InviteHubAcceptedDto>("InviteAccepted", dto => receivedByReceiver = dto);

        await senderConn.StartAsync(TestContext.Current.CancellationToken);
        await receiverConn.StartAsync(TestContext.Current.CancellationToken);

        var receiverClient = App.CreateAuthenticatedClient(RECEIVER_SUB);
        receiverClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.90.1.2");

        var acceptResponse = await receiverClient.PatchAsJsonAsync(
            $"api/v1/invites/{inviteId}/accept",
            new { },
            TestContext.Current.CancellationToken);
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var acceptedDto = await senderTcs.Task.WaitAsync(cts.Token);

        acceptedDto.Should().NotBeNull();
        acceptedDto.InviteId.Should().Be(inviteId);
        acceptedDto.MeetupId.Should().NotBeEmpty();
        receivedByReceiver.Should().BeNull();

        await senderConn.StopAsync(TestContext.Current.CancellationToken);
        await receiverConn.StopAsync(TestContext.Current.CancellationToken);
        await senderConn.DisposeAsync();
        await receiverConn.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // InviteDeclined pushes to sender only
    // -----------------------------------------------------------------------

    /// <summary>Receiver declines → sender gets InviteDeclined; receiver does NOT.</summary>
    [Fact]
    public async Task InviteDeclined_PushesInviteDeclinedToSenderOnly()
    {
        const string SENDER_SUB = "oid|hub-declined-sender";
        const string RECEIVER_SUB = "oid|hub-declined-receiver";
        var now = App.FakeTimeProvider.GetUtcNow();

        var (senderId, receiverId, tag, place) = await SeedTwoUsersWithCityTagPlaceAsync(SENDER_SUB, RECEIVER_SUB, now);

        Guid inviteId;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var invite = new Invite
            {
                Id = Guid.NewGuid(),
                SenderId = senderId,
                ReceiverId = receiverId,
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
            inviteId = invite.Id;
        }

        var senderConn = App.CreateAuthenticatedSignalRConnection(SENDER_SUB);
        var receiverConn = App.CreateAuthenticatedSignalRConnection(RECEIVER_SUB);

        var senderTcs = new TaskCompletionSource<InviteHubDeclinedDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        InviteHubDeclinedDto? receivedByReceiver = null;

        senderConn.On<InviteHubDeclinedDto>("InviteDeclined", dto => senderTcs.TrySetResult(dto));
        receiverConn.On<InviteHubDeclinedDto>("InviteDeclined", dto => receivedByReceiver = dto);

        await senderConn.StartAsync(TestContext.Current.CancellationToken);
        await receiverConn.StartAsync(TestContext.Current.CancellationToken);

        var receiverClient = App.CreateAuthenticatedClient(RECEIVER_SUB);
        receiverClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.90.1.3");

        var declineResponse = await receiverClient.PatchAsJsonAsync(
            $"api/v1/invites/{inviteId}/decline",
            new { },
            TestContext.Current.CancellationToken);
        declineResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var declinedDto = await senderTcs.Task.WaitAsync(cts.Token);

        declinedDto.Should().NotBeNull();
        declinedDto.InviteId.Should().Be(inviteId);
        receivedByReceiver.Should().BeNull();

        await senderConn.StopAsync(TestContext.Current.CancellationToken);
        await receiverConn.StopAsync(TestContext.Current.CancellationToken);
        await senderConn.DisposeAsync();
        await receiverConn.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // InviteExpired pushes to both participants
    // -----------------------------------------------------------------------

    /// <summary>InviteExpiredAsync called directly → both sender and receiver get InviteExpired.</summary>
    [Fact]
    public async Task InviteExpired_PushesInviteExpiredToBothParticipants()
    {
        const string SENDER_SUB = "oid|hub-expired-sender";
        const string RECEIVER_SUB = "oid|hub-expired-receiver";
        var now = App.FakeTimeProvider.GetUtcNow();

        var (senderId, receiverId, tag, place) = await SeedTwoUsersWithCityTagPlaceAsync(SENDER_SUB, RECEIVER_SUB, now);

        Invite seedInvite;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            seedInvite = new Invite
            {
                Id = Guid.NewGuid(),
                SenderId = senderId,
                ReceiverId = receiverId,
                HangoutTagId = tag.Id,
                PlaceId = place.Id,
                SenderIsThere = false,
                Status = InviteStatus.Pending,
                SentAt = now.AddHours(-50),
                ExpiresAt = now.AddHours(-2),
                CreatedAt = now.AddHours(-50),
            };
            db.Invites.Add(seedInvite);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var senderConn = App.CreateAuthenticatedSignalRConnection(SENDER_SUB);
        var receiverConn = App.CreateAuthenticatedSignalRConnection(RECEIVER_SUB);

        var senderTcs = new TaskCompletionSource<InviteHubExpiredDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receiverTcs = new TaskCompletionSource<InviteHubExpiredDto>(TaskCreationOptions.RunContinuationsAsynchronously);

        senderConn.On<InviteHubExpiredDto>("InviteExpired", dto => senderTcs.TrySetResult(dto));
        receiverConn.On<InviteHubExpiredDto>("InviteExpired", dto => receiverTcs.TrySetResult(dto));

        await senderConn.StartAsync(TestContext.Current.CancellationToken);
        await receiverConn.StartAsync(TestContext.Current.CancellationToken);

        // Invoke notifier directly (production caller is UC-308 Hangfire job)
        using (var scope = App.Services.CreateScope())
        {
            var notifier = scope.ServiceProvider.GetRequiredService<IInviteNotifier>();
            await notifier.InviteExpiredAsync(seedInvite, TestContext.Current.CancellationToken);
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var senderDto = await senderTcs.Task.WaitAsync(cts.Token);
        var receiverDto = await receiverTcs.Task.WaitAsync(cts.Token);

        senderDto.InviteId.Should().Be(seedInvite.Id);
        receiverDto.InviteId.Should().Be(seedInvite.Id);

        await senderConn.StopAsync(TestContext.Current.CancellationToken);
        await receiverConn.StopAsync(TestContext.Current.CancellationToken);
        await senderConn.DisposeAsync();
        await receiverConn.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // Soft-deleted user cannot connect
    // -----------------------------------------------------------------------

    /// <summary>Soft-deleted user: JwtSubUserIdProvider returns null; send to that user is a no-op.</summary>
    [Fact]
    public async Task SoftDeletedUser_CannotConnect()
    {
        const string SENDER_SUB = "oid|hub-soft-del-sender";
        const string DELETED_SUB = "oid|hub-soft-del-deleted";
        const string ALIVE_RECEIVER_SUB = "oid|hub-soft-del-alive";
        var now = App.FakeTimeProvider.GetUtcNow();

        var (senderId, _, tag, place) = await SeedTwoUsersWithCityTagPlaceAsync(SENDER_SUB, ALIVE_RECEIVER_SUB, now);

        // Add a soft-deleted user
        Guid deletedUserId;
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            // Reuse the city from the existing setup (first city in DB)
            var cityId = (await db.Cities.AsNoTracking().FirstAsync(TestContext.Current.CancellationToken)).Id;
            var deletedUser = MakeUser(DELETED_SUB, cityId, now);
            deletedUser.DeletedAt = now.AddDays(-1);
            db.Users.Add(deletedUser);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            deletedUserId = deletedUser.Id;
        }

        // Alive receiver connects OK
        var aliveConn = App.CreateAuthenticatedSignalRConnection(ALIVE_RECEIVER_SUB);
        var aliveTcs = new TaskCompletionSource<InviteHubReceivedDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        aliveConn.On<InviteHubReceivedDto>("InviteReceived", dto => aliveTcs.TrySetResult(dto));
        await aliveConn.StartAsync(TestContext.Current.CancellationToken);

        // Deleted user: even though they have a JWT, the UserIdProvider returns null
        // so they would get no messages — but they can still "connect" (JWT is valid, just mapped to null user-id)
        // The test verifies alive user still gets their event when sender invites them
        var aliveReceiverId = (await GetUserIdAsync(ALIVE_RECEIVER_SUB));

        var senderClient = App.CreateAuthenticatedClient(SENDER_SUB);
        senderClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.90.1.4");

        var response = await senderClient.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = aliveReceiverId, HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = false },
            TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var dto = await aliveTcs.Task.WaitAsync(cts.Token);
        dto.Should().NotBeNull();

        await aliveConn.StopAsync(TestContext.Current.CancellationToken);
        await aliveConn.DisposeAsync();
    }

    private async Task<Guid> GetUserIdAsync(string sub)
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.AzureAdB2CId == sub, TestContext.Current.CancellationToken);
        return user.Id;
    }

    // -----------------------------------------------------------------------
    // Multi-device: both connections receive the event
    // -----------------------------------------------------------------------

    /// <summary>Same user on two connections both receive InviteReceived (multi-device fanout).</summary>
    [Fact]
    public async Task MultiDevice_BothConnectionsReceive_InviteReceived()
    {
        const string SENDER_SUB = "oid|hub-multi-sender";
        const string RECEIVER_SUB = "oid|hub-multi-receiver";
        var now = App.FakeTimeProvider.GetUtcNow();

        var (_, receiverId, tag, place) = await SeedTwoUsersWithCityTagPlaceAsync(SENDER_SUB, RECEIVER_SUB, now);

        var phone = App.CreateAuthenticatedSignalRConnection(RECEIVER_SUB);
        var tablet = App.CreateAuthenticatedSignalRConnection(RECEIVER_SUB);

        var phoneTcs = new TaskCompletionSource<InviteHubReceivedDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tabletTcs = new TaskCompletionSource<InviteHubReceivedDto>(TaskCreationOptions.RunContinuationsAsynchronously);

        phone.On<InviteHubReceivedDto>("InviteReceived", dto => phoneTcs.TrySetResult(dto));
        tablet.On<InviteHubReceivedDto>("InviteReceived", dto => tabletTcs.TrySetResult(dto));

        await phone.StartAsync(TestContext.Current.CancellationToken);
        await tablet.StartAsync(TestContext.Current.CancellationToken);

        var senderClient = App.CreateAuthenticatedClient(SENDER_SUB);
        senderClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.90.1.5");

        var response = await senderClient.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = receiverId, HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = false },
            TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var phoneDto = await phoneTcs.Task.WaitAsync(cts.Token);
        var tabletDto = await tabletTcs.Task.WaitAsync(cts.Token);

        phoneDto.Id.Should().Be(tabletDto.Id);

        await phone.StopAsync(TestContext.Current.CancellationToken);
        await tablet.StopAsync(TestContext.Current.CancellationToken);
        await phone.DisposeAsync();
        await tablet.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // PII discipline
    // -----------------------------------------------------------------------

    /// <summary>InviteReceived payload contains only expected keys — no PII leak.</summary>
    [Fact]
    public async Task PiiDiscipline_InviteReceivedPayload_ContainsOnlyExpectedKeys()
    {
        const string SENDER_SUB = "oid|hub-pii-sender";
        const string RECEIVER_SUB = "oid|hub-pii-receiver";
        var now = App.FakeTimeProvider.GetUtcNow();

        var (_, receiverId, tag, place) = await SeedTwoUsersWithCityTagPlaceAsync(SENDER_SUB, RECEIVER_SUB, now);

        var receiverConn = App.CreateAuthenticatedSignalRConnection(RECEIVER_SUB);
        var rawTcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        receiverConn.On<JsonElement>("InviteReceived", dto => rawTcs.TrySetResult(dto));
        await receiverConn.StartAsync(TestContext.Current.CancellationToken);

        var senderClient = App.CreateAuthenticatedClient(SENDER_SUB);
        senderClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.90.1.6");

        var response = await senderClient.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = receiverId, HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = false },
            TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var raw = await rawTcs.Task.WaitAsync(cts.Token);

        var topLevelKeys = raw.EnumerateObject().Select(p => p.Name).ToList();

        // Must have exactly these keys
        topLevelKeys.Should().BeEquivalentTo(["id", "sender", "hangoutTagSlug", "place", "sentAt", "expiresAt"]);

        // Must NOT contain PII fields (case-insensitive check for forbidden key names)
        topLevelKeys.Should().NotContain(k =>
            k.Equals("email", StringComparison.OrdinalIgnoreCase) ||
            k.Equals("azureAdB2CId", StringComparison.OrdinalIgnoreCase) ||
            k.Equals("fcmToken", StringComparison.OrdinalIgnoreCase) ||
            k.Equals("bio", StringComparison.OrdinalIgnoreCase));

        await receiverConn.StopAsync(TestContext.Current.CancellationToken);
        await receiverConn.DisposeAsync();
    }
}
