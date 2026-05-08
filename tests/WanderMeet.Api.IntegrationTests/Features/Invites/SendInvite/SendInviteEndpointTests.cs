using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Invites.SendInvite;
using WanderMeet.Api.Features.Invites.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared;
using WanderMeet.Shared.Enums;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Invites.SendInvite;

/// <summary>Integration tests for POST /api/v1/invites.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class SendInviteEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private const double CityLon = 14.42;
    private const double CityLat = 50.08;

    private static Point CityCenter() => new(CityLon, CityLat) { SRID = 4326 };

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static User MakeUser(string sub, Guid? cityId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        AzureAdB2CId = sub,
        FirstName = "User " + sub,
        CityId = cityId,
        CreatedAt = now,
        LastActiveAt = now,
    };

    private static City MakeCity(DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test City",
        Country = "CZ",
        Location = CityCenter(),
        CreatedAt = now,
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

    // -----------------------------------------------------------------------
    // Auth / user guard tests
    // -----------------------------------------------------------------------

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearerToken_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.1.1");

        var response = await client.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = Guid.NewGuid(), HangoutTagId = Guid.NewGuid(), PlaceId = Guid.NewGuid(), SenderIsThere = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>JWT sub has no User row → 404 + User.NotRegistered.</summary>
    [Fact]
    public async Task HandleAsync_JwtSubHasNoUserRow_Returns404WithUserNotRegistered()
    {
        const string SUB = "oid|invite-send-no-user";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.1.2");

        var response = await client.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = Guid.NewGuid(), HangoutTagId = Guid.NewGuid(), PlaceId = Guid.NewGuid(), SenderIsThere = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.User.NotRegistered);
    }

    // -----------------------------------------------------------------------
    // Validation (400) tests
    // -----------------------------------------------------------------------

    // Empty Guid validator failures are covered by the validator unit tests
    // (SendInviteValidatorTests). The integration-level tests here would assert
    // the error code is in the JSON body — but FastEndpoints' default response
    // shape uses property names as keys, not error codes (per CLAUDE.md trap).
    // Asserting that here would test the framework, not our slice.


    /// <summary>Receiver equals caller → 400 + Invite.SelfInviteForbidden.</summary>
    [Fact]
    public async Task HandleAsync_ReceiverEqualsCaller_Returns400WithSelfInviteForbidden()
    {
        const string SUB = "oid|invite-send-self";
        var now = App.FakeTimeProvider.GetUtcNow();

        var callerId = await SeedCallerAsync(SUB, cityId: null, now);
        var tag = MakeHangoutTag(now);
        var placeCityId = Guid.NewGuid();
        var place = MakePlace(placeCityId, now);

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Cities.Add(new City { Id = placeCityId, Name = "Test City", Country = "CZ", Location = CityCenter(), CreatedAt = now });
            db.HangoutTags.Add(tag);
            db.Places.Add(place);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.1.6");

        var response = await client.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = callerId, HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Invite.SelfInviteForbidden);
    }

    /// <summary>Unknown receiver → 400 + Invite.ReceiverNotFound.</summary>
    [Fact]
    public async Task HandleAsync_UnknownReceiver_Returns400WithReceiverNotFound()
    {
        const string SUB = "oid|invite-send-unknown-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        await SeedCallerAsync(SUB, cityId: null, now);
        var tag = MakeHangoutTag(now);
        var placeCityId = Guid.NewGuid();
        var place = MakePlace(placeCityId, now);

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Cities.Add(new City { Id = placeCityId, Name = "Test City", Country = "CZ", Location = CityCenter(), CreatedAt = now });
            db.HangoutTags.Add(tag);
            db.Places.Add(place);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.1.7");

        var response = await client.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = Guid.NewGuid(), HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Invite.ReceiverNotFound);
    }

    /// <summary>Soft-deleted receiver → 400 + Invite.ReceiverNotFound.</summary>
    [Fact]
    public async Task HandleAsync_SoftDeletedReceiver_Returns400WithReceiverNotFound()
    {
        const string CALLER_SUB = "oid|invite-send-del-recv-caller";
        const string RECEIVER_SUB = "oid|invite-send-del-recv-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        await SeedCallerAsync(CALLER_SUB, cityId: null, now);
        var tag = MakeHangoutTag(now);
        var placeCityId = Guid.NewGuid();
        var place = MakePlace(placeCityId, now);
        Guid receiverId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Cities.Add(new City { Id = placeCityId, Name = "Test City", Country = "CZ", Location = CityCenter(), CreatedAt = now });
            var receiver = MakeUser(RECEIVER_SUB, null, now);
            receiver.DeletedAt = now;
            receiverId = receiver.Id;
            db.Users.Add(receiver);
            db.HangoutTags.Add(tag);
            db.Places.Add(place);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.1.8");

        var response = await client.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = receiverId, HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Invite.ReceiverNotFound);
    }

    /// <summary>Unknown hangout tag → 400 + Invite.HangoutTagNotFound.</summary>
    [Fact]
    public async Task HandleAsync_UnknownHangoutTag_Returns400WithHangoutTagNotFound()
    {
        const string CALLER_SUB = "oid|invite-send-no-tag-caller";
        const string RECEIVER_SUB = "oid|invite-send-no-tag-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        var cityId = Guid.NewGuid();
        await SeedCallerAndReceiverAsync(CALLER_SUB, RECEIVER_SUB, cityId, now);
        var place = MakePlace(cityId, now);

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.Places.Add(place);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.1.9");

        var response = await client.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = await GetReceiverIdAsync(RECEIVER_SUB), HangoutTagId = Guid.NewGuid(), PlaceId = place.Id, SenderIsThere = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Invite.HangoutTagNotFound);
    }

    /// <summary>Unknown place → 400 + Invite.PlaceNotFound.</summary>
    [Fact]
    public async Task HandleAsync_UnknownPlace_Returns400WithPlaceNotFound()
    {
        const string CALLER_SUB = "oid|invite-send-no-place-caller";
        const string RECEIVER_SUB = "oid|invite-send-no-place-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        var cityId = Guid.NewGuid();
        await SeedCallerAndReceiverAsync(CALLER_SUB, RECEIVER_SUB, cityId, now);
        var tag = MakeHangoutTag(now);

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.HangoutTags.Add(tag);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.1.10");

        var response = await client.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = await GetReceiverIdAsync(RECEIVER_SUB), HangoutTagId = tag.Id, PlaceId = Guid.NewGuid(), SenderIsThere = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Invite.PlaceNotFound);
    }

    /// <summary>Place city does not match receiver's city → 400 + Invite.PlaceCityMismatch.</summary>
    [Fact]
    public async Task HandleAsync_PlaceCityDoesNotMatchReceiverCity_Returns400WithPlaceCityMismatch()
    {
        const string CALLER_SUB = "oid|invite-send-mismatch-caller";
        const string RECEIVER_SUB = "oid|invite-send-mismatch-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        var receiverCityId = Guid.NewGuid();
        var differentCityId = Guid.NewGuid();

        await SeedCallerAndReceiverAsync(CALLER_SUB, RECEIVER_SUB, receiverCityId, now);
        var tag = MakeHangoutTag(now);
        var placeInDifferentCity = MakePlace(differentCityId, now);

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            // Seed the second City so the Place's CityId FK resolves.
            db.Cities.Add(new City
            {
                Id = differentCityId,
                Name = "Other City",
                Country = "CZ",
                Location = CityCenter(),
                CreatedAt = now,
            });
            db.HangoutTags.Add(tag);
            db.Places.Add(placeInDifferentCity);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.1.11");

        var response = await client.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = await GetReceiverIdAsync(RECEIVER_SUB), HangoutTagId = tag.Id, PlaceId = placeInDifferentCity.Id, SenderIsThere = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Invite.PlaceCityMismatch);
    }

    // -----------------------------------------------------------------------
    // Conflict (409) tests
    // -----------------------------------------------------------------------

    /// <summary>Pending invite from caller to receiver already exists → 409 + Invite.AlreadyPending.</summary>
    [Fact]
    public async Task HandleAsync_PendingInviteFromCallerToReceiverExists_Returns409WithAlreadyPending()
    {
        const string CALLER_SUB = "oid|invite-send-pending-c2r-caller";
        const string RECEIVER_SUB = "oid|invite-send-pending-c2r-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        var cityId = Guid.NewGuid();
        var (callerId, receiverId) = await SeedCallerAndReceiverAsync(CALLER_SUB, RECEIVER_SUB, cityId, now);
        var tag = MakeHangoutTag(now);
        var place = MakePlace(cityId, now);

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.HangoutTags.Add(tag);
            db.Places.Add(place);
            db.Invites.Add(MakePendingInvite(callerId, receiverId, tag.Id, place.Id, now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.1.12");

        var response = await client.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = receiverId, HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Invite.AlreadyPending);
    }

    /// <summary>Pending invite from receiver to caller (reverse direction) → 409 + Invite.AlreadyPending.</summary>
    [Fact]
    public async Task HandleAsync_PendingInviteFromReceiverToCallerExists_Returns409WithAlreadyPending()
    {
        const string CALLER_SUB = "oid|invite-send-pending-r2c-caller";
        const string RECEIVER_SUB = "oid|invite-send-pending-r2c-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        var cityId = Guid.NewGuid();
        var (callerId, receiverId) = await SeedCallerAndReceiverAsync(CALLER_SUB, RECEIVER_SUB, cityId, now);
        var tag = MakeHangoutTag(now);
        var place = MakePlace(cityId, now);

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.HangoutTags.Add(tag);
            db.Places.Add(place);
            // Reverse direction: receiver→caller
            db.Invites.Add(MakePendingInvite(receiverId, callerId, tag.Id, place.Id, now));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.1.13");

        var response = await client.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = receiverId, HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Invite.AlreadyPending);
    }

    /// <summary>Previous Declined invite from caller → resend is allowed and returns 201.</summary>
    [Fact]
    public async Task HandleAsync_PreviousDeclinedInviteFromCaller_AllowsResendAndReturns201()
    {
        const string CALLER_SUB = "oid|invite-send-declined-resend-caller";
        const string RECEIVER_SUB = "oid|invite-send-declined-resend-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        var cityId = Guid.NewGuid();
        var (callerId, receiverId) = await SeedCallerAndReceiverAsync(CALLER_SUB, RECEIVER_SUB, cityId, now);
        var tag = MakeHangoutTag(now);
        var place = MakePlace(cityId, now);

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.HangoutTags.Add(tag);
            db.Places.Add(place);
            // Seed a declined invite
            db.Invites.Add(new Invite
            {
                Id = Guid.NewGuid(),
                SenderId = callerId,
                ReceiverId = receiverId,
                HangoutTagId = tag.Id,
                PlaceId = place.Id,
                SenderIsThere = false,
                Status = InviteStatus.Declined,
                SentAt = now.AddHours(-10),
                RespondedAt = now.AddHours(-9),
                ExpiresAt = now.AddHours(-10) + ValidationConstants.InviteExpiryWindow,
                CreatedAt = now.AddHours(-10),
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.1.14");

        var response = await client.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = receiverId, HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // -----------------------------------------------------------------------
    // Happy path tests
    // -----------------------------------------------------------------------

    /// <summary>Happy path: 201 with InviteDto, DB row persisted correctly, no Meetup row.</summary>
    [Fact]
    public async Task HandleAsync_HappyPath_Returns201WithInviteDtoStatusPendingExpiresAtSentPlus48h()
    {
        const string CALLER_SUB = "oid|invite-send-happy-caller";
        const string RECEIVER_SUB = "oid|invite-send-happy-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        var cityId = Guid.NewGuid();
        var (callerId, receiverId) = await SeedCallerAndReceiverAsync(CALLER_SUB, RECEIVER_SUB, cityId, now);
        var tag = MakeHangoutTag(now);
        var place = MakePlace(cityId, now);

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.HangoutTags.Add(tag);
            db.Places.Add(place);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.1.15");

        var response = await client.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = receiverId, HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = true },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var dto = await response.Content.ReadFromJsonAsync<InviteDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        dto.Should().NotBeNull();
        dto!.Status.Should().Be(InviteStatus.Pending);
        dto.SentAt.Should().Be(now);
        dto.ExpiresAt.Should().Be(now + ValidationConstants.InviteExpiryWindow);
        dto.RespondedAt.Should().BeNull();
        dto.SenderIsThere.Should().BeTrue();
        dto.HangoutTagId.Should().Be(tag.Id);
        dto.Sender.Id.Should().Be(callerId);
        dto.Receiver.Id.Should().Be(receiverId);

        // DB row assertions
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();

            var invite = await db.Invites.AsNoTracking()
                .FirstOrDefaultAsync(i => i.SenderId == callerId && i.ReceiverId == receiverId,
                    TestContext.Current.CancellationToken);

            invite.Should().NotBeNull();
            invite!.Status.Should().Be(InviteStatus.Pending);
            invite.SentAt.Should().Be(now);
            invite.ExpiresAt.Should().Be(now + ValidationConstants.InviteExpiryWindow);
            invite.PlaceId.Should().Be(place.Id);
            invite.HangoutTagId.Should().Be(tag.Id);

            // No Meetup row
            var meetupCount = await db.Meetups.CountAsync(TestContext.Current.CancellationToken);
            meetupCount.Should().Be(0);

            // LastActiveAt updated
            var caller = await db.Users.AsNoTracking().FirstAsync(u => u.Id == callerId, TestContext.Current.CancellationToken);
            caller.LastActiveAt.Should().Be(now);
        }
    }

    /// <summary>Happy path with a recording notifier: InviteSentAsync is invoked exactly once with the persisted invite.</summary>
    [Fact]
    public async Task HandleAsync_HappyPath_FiresInviteSentAsyncOnInviteNotifier()
    {
        const string CALLER_SUB = "oid|invite-send-fire-caller";
        const string RECEIVER_SUB = "oid|invite-send-fire-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        var cityId = Guid.NewGuid();
        var (callerId, receiverId) = await SeedCallerAndReceiverAsync(CALLER_SUB, RECEIVER_SUB, cityId, now);
        var tag = MakeHangoutTag(now);
        var place = MakePlace(cityId, now);

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.HangoutTags.Add(tag);
            db.Places.Add(place);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var spy = new RecordingInviteNotifier();
        var client = App.CreateAuthenticatedClientWithInviteNotifier(CALLER_SUB, spy);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.1.16");

        var response = await client.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = receiverId, HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        spy.Sent.Should().HaveCount(1);
        spy.Sent[0].SenderId.Should().Be(callerId);
        spy.Sent[0].ReceiverId.Should().Be(receiverId);
        spy.Accepted.Should().BeEmpty();
    }

    /// <summary>Notifier throws on send → endpoint still returns 201 and invite is persisted (resilience).</summary>
    [Fact]
    public async Task HandleAsync_NotifierThrows_StillReturns201AndPersistsInvite()
    {
        const string CALLER_SUB = "oid|invite-send-notifier-throws-caller";
        const string RECEIVER_SUB = "oid|invite-send-notifier-throws-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        var cityId = Guid.NewGuid();
        var (callerId, receiverId) = await SeedCallerAndReceiverAsync(CALLER_SUB, RECEIVER_SUB, cityId, now);
        var tag = MakeHangoutTag(now);
        var place = MakePlace(cityId, now);

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.HangoutTags.Add(tag);
            db.Places.Add(place);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var spy = new RecordingInviteNotifier { ThrowOnSent = new InvalidOperationException("simulated downstream failure") };
        var client = App.CreateAuthenticatedClientWithInviteNotifier(CALLER_SUB, spy);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.1.17");

        var response = await client.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = receiverId, HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = false },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var exists = await db.Invites.AsNoTracking().AnyAsync(
                i => i.SenderId == callerId && i.ReceiverId == receiverId,
                TestContext.Current.CancellationToken);
            exists.Should().BeTrue();
        }
    }

    /// <summary>Happy path: TrustScore unchanged for caller and receiver.</summary>
    [Fact]
    public async Task HandleAsync_HappyPath_DoesNotChangeUserTrustScore()
    {
        const string CALLER_SUB = "oid|invite-send-trust-caller";
        const string RECEIVER_SUB = "oid|invite-send-trust-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        var cityId = Guid.NewGuid();
        var (callerId, receiverId) = await SeedCallerAndReceiverAsync(CALLER_SUB, RECEIVER_SUB, cityId, now);
        var tag = MakeHangoutTag(now);
        var place = MakePlace(cityId, now);

        int callerTrustBefore, receiverTrustBefore;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.HangoutTags.Add(tag);
            db.Places.Add(place);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            callerTrustBefore = (await db.Users.AsNoTracking().FirstAsync(u => u.Id == callerId, TestContext.Current.CancellationToken)).TrustScore;
            receiverTrustBefore = (await db.Users.AsNoTracking().FirstAsync(u => u.Id == receiverId, TestContext.Current.CancellationToken)).TrustScore;
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.1.17");

        await client.PostAsJsonAsync(
            "api/v1/invites",
            new { ReceiverId = receiverId, HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = false },
            TestContext.Current.CancellationToken);

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var caller = await db.Users.AsNoTracking().FirstAsync(u => u.Id == callerId, TestContext.Current.CancellationToken);
            var receiver = await db.Users.AsNoTracking().FirstAsync(u => u.Id == receiverId, TestContext.Current.CancellationToken);
            caller.TrustScore.Should().Be(callerTrustBefore);
            receiver.TrustScore.Should().Be(receiverTrustBefore);
        }
    }

    /// <summary>Rate limit exceeded (21 calls in same session) → 429 with Retry-After.</summary>
    [Fact]
    public async Task HandleAsync_RateLimitExceeded_Returns429WithRetryAfter()
    {
        const string RATE_LIMIT_TEST_IP = "10.30.50.1";
        const string CALLER_SUB = "oid|invite-send-ratelimit-caller";
        const string RECEIVER_SUB = "oid|invite-send-ratelimit-recv";
        var now = App.FakeTimeProvider.GetUtcNow();

        var cityId = Guid.NewGuid();
        var (callerId, receiverId) = await SeedCallerAndReceiverAsync(CALLER_SUB, RECEIVER_SUB, cityId, now);
        var tag = MakeHangoutTag(now);
        var place = MakePlace(cityId, now);

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            db.HangoutTags.Add(tag);
            db.Places.Add(place);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // IMPORTANT: build client ONCE outside the loop so all calls hit the same WAF instance
        // (each WithWebHostBuilder creates a new derived factory with its own rate-limit counters)
        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", RATE_LIMIT_TEST_IP);

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i <= 20; i++)
        {
            lastResponse = await client.PostAsJsonAsync(
                "api/v1/invites",
                new { ReceiverId = receiverId, HangoutTagId = tag.Id, PlaceId = place.Id, SenderIsThere = false },
                TestContext.Current.CancellationToken);
        }

        lastResponse.Should().NotBeNull();
        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        lastResponse.Headers.Contains("Retry-After").Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private async Task<Guid> SeedCallerAsync(string sub, Guid? cityId, DateTimeOffset now)
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var caller = MakeUser(sub, cityId, now);
        db.Users.Add(caller);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return caller.Id;
    }

    private async Task<(Guid callerId, Guid receiverId)> SeedCallerAndReceiverAsync(
        string callerSub, string receiverSub, Guid cityId, DateTimeOffset now)
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        // Seed the City row first so the User.CityId FK resolves.
        if (!await db.Cities.AsNoTracking().AnyAsync(c => c.Id == cityId, TestContext.Current.CancellationToken))
        {
            db.Cities.Add(new City
            {
                Id = cityId,
                Name = "Test City",
                Country = "CZ",
                Location = CityCenter(),
                CreatedAt = now,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var caller = MakeUser(callerSub, cityId, now);
        var receiver = MakeUser(receiverSub, cityId, now);
        db.Users.Add(caller);
        db.Users.Add(receiver);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (caller.Id, receiver.Id);
    }

    private async Task<Guid> GetReceiverIdAsync(string sub)
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.AzureAdB2CId == sub, TestContext.Current.CancellationToken);
        return user.Id;
    }

    private static Invite MakePendingInvite(Guid senderId, Guid receiverId, Guid tagId, Guid placeId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            SenderId = senderId,
            ReceiverId = receiverId,
            HangoutTagId = tagId,
            PlaceId = placeId,
            SenderIsThere = false,
            Status = InviteStatus.Pending,
            SentAt = now,
            ExpiresAt = now + ValidationConstants.InviteExpiryWindow,
            CreatedAt = now,
        };
}
