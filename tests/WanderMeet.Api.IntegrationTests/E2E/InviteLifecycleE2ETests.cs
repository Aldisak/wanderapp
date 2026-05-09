using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared.Enums;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.E2E;

/// <summary>End-to-end integration test for the full invite lifecycle: send → accept → review → trust-score updated.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class InviteLifecycleE2ETests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    // Deterministic Guids for seed data
    private static readonly Guid AliceId   = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid BobId     = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid CityId    = new("00000000-0000-0000-0000-000000000003");
    private static readonly Guid PlaceId   = new("00000000-0000-0000-0000-000000000004");
    private static readonly Guid TagId     = new("00000000-0000-0000-0000-000000000005");

    private const string AliceSub = "oid|e2e-alice";
    private const string BobSub   = "oid|e2e-bob";
    private const string PlaceName = "E2E Cafe";

    private static Point CityCenter() => new(14.42, 50.08) { SRID = 4326 };

    // -----------------------------------------------------------------------
    // E2E: Send → Accept → Review (Alice) → Review (Bob) → DB assertions
    // -----------------------------------------------------------------------

    /// <summary>
    /// Full invite lifecycle end-to-end: Alice sends, Bob accepts, both submit reviews.
    /// Asserts trust-score update, place meetup count increment, and FCM push notifications.
    /// </summary>
    [Fact]
    public async Task EndToEnd_SendAcceptReview_TrustScoreUpdated()
    {
        var ct = TestContext.Current.CancellationToken;
        var now = App.FakeTimeProvider.GetUtcNow();

        // -----------------------------------------------------------------------
        // Seed: Alice + Bob + city + place + coffee tag
        // -----------------------------------------------------------------------
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();

            db.Cities.Add(new City
            {
                Id = CityId,
                Name = "E2E City",
                Country = "CZ",
                Location = CityCenter(),
                CreatedAt = now,
            });

            db.HangoutTags.Add(new HangoutTag
            {
                Id = TagId,
                Slug = HangoutTagSlug.Coffee,
                Label = "Coffee",
                Emoji = "☕",
                CreatedAt = now,
            });

            db.Places.Add(new Place
            {
                Id = PlaceId,
                GooglePlaceId = "e2e-gp-001",
                Name = PlaceName,
                CityId = CityId,
                Location = CityCenter(),
                Category = PlaceCategory.Cafe,
                CreatedAt = now,
            });

            db.Users.Add(new User
            {
                Id = AliceId,
                AzureAdB2CId = AliceSub,
                FirstName = "Alice",
                CityId = CityId,
                FcmToken = "alice-fcm",
                CreatedAt = now,
                LastActiveAt = now,
            });

            db.Users.Add(new User
            {
                Id = BobId,
                AzureAdB2CId = BobSub,
                FirstName = "Bob",
                CityId = CityId,
                FcmToken = "bob-fcm",
                CreatedAt = now,
                LastActiveAt = now,
            });

            await db.SaveChangesAsync(ct);
        }

        var aliceClient = App.CreateAuthenticatedClient(AliceSub);
        var bobClient   = App.CreateAuthenticatedClient(BobSub);

        // -----------------------------------------------------------------------
        // Step 1: Alice POSTs /api/v1/invites → 201
        // -----------------------------------------------------------------------
        aliceClient.DefaultRequestHeaders.Remove("X-Forwarded-For");
        aliceClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.120.1.1");

        var sendResponse = await aliceClient.PostAsJsonAsync(
            "api/v1/invites",
            new
            {
                ReceiverId = BobId,
                HangoutTagId = TagId,
                PlaceId = PlaceId,
                SenderIsThere = false,
            },
            ct);

        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendBody = await sendResponse.Content.ReadFromJsonAsync<InviteDtoLocal>(cancellationToken: ct);
        sendBody.Should().NotBeNull();
        var inviteId = sendBody!.Id;
        inviteId.Should().NotBeEmpty();

        // FCM push to Bob's token with "Coffee at {placeName}?" title
        App.FcmClient.Sends.Should().ContainSingle(s =>
            s.Token == "bob-fcm" && s.Title == $"Coffee at {PlaceName}?");

        // -----------------------------------------------------------------------
        // Step 2: Bob PATCHes /api/v1/invites/{id}/accept → 200
        // -----------------------------------------------------------------------
        bobClient.DefaultRequestHeaders.Remove("X-Forwarded-For");
        bobClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.120.1.2");

        var acceptResponse = await bobClient.PatchAsJsonAsync(
            $"api/v1/invites/{inviteId}/accept",
            new { },
            ct);

        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var acceptBody = await acceptResponse.Content.ReadFromJsonAsync<AcceptInviteDtoLocal>(cancellationToken: ct);
        acceptBody.Should().NotBeNull();
        var meetupId = acceptBody!.MeetupId;
        meetupId.Should().NotBeEmpty();

        // FCM push to Alice's token with "See you there!" title
        App.FcmClient.Sends.Should().Contain(s =>
            s.Token == "alice-fcm" && s.Title == "See you there!");

        // -----------------------------------------------------------------------
        // Step 3: Alice POSTs /api/v1/meetups/{id}/review (all-positive, didMeet=true) → 200
        // -----------------------------------------------------------------------
        aliceClient.DefaultRequestHeaders.Remove("X-Forwarded-For");
        aliceClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.120.1.3");

        var aliceReviewResponse = await aliceClient.PostAsJsonAsync(
            $"api/v1/meetups/{meetupId}/review",
            new
            {
                DidMeet = true,
                FeltSafe = true,
                GoodConvo = true,
                WouldMeetAgain = true,
                Text = (string?)null,
            },
            ct);

        aliceReviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var aliceReviewBody = await aliceReviewResponse.Content.ReadFromJsonAsync<SubmitReviewDtoLocal>(cancellationToken: ct);
        aliceReviewBody.Should().NotBeNull();
        aliceReviewBody!.Reviewee.TrustScore.Should().Be(15);
        aliceReviewBody.Reviewee.MeetupCount.Should().Be(1);

        // -----------------------------------------------------------------------
        // Step 4: Bob POSTs the symmetric review → 200
        // -----------------------------------------------------------------------
        bobClient.DefaultRequestHeaders.Remove("X-Forwarded-For");
        bobClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.120.1.4");

        var bobReviewResponse = await bobClient.PostAsJsonAsync(
            $"api/v1/meetups/{meetupId}/review",
            new
            {
                DidMeet = true,
                FeltSafe = true,
                GoodConvo = true,
                WouldMeetAgain = true,
                Text = (string?)null,
            },
            ct);

        bobReviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var bobReviewBody = await bobReviewResponse.Content.ReadFromJsonAsync<SubmitReviewDtoLocal>(cancellationToken: ct);
        bobReviewBody.Should().NotBeNull();
        bobReviewBody!.Reviewee.TrustScore.Should().Be(15);
        bobReviewBody.Reviewee.MeetupCount.Should().Be(1);

        // -----------------------------------------------------------------------
        // Step 5: DB assertions (AsNoTracking)
        // -----------------------------------------------------------------------
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();

            var alice = await db.Users.AsNoTracking()
                .FirstAsync(u => u.Id == AliceId, ct);
            alice.TrustScore.Should().Be(15);
            alice.MeetupCount.Should().Be(1);

            var bob = await db.Users.AsNoTracking()
                .FirstAsync(u => u.Id == BobId, ct);
            bob.TrustScore.Should().Be(15);
            bob.MeetupCount.Should().Be(1);

            var place = await db.Places.AsNoTracking()
                .FirstAsync(p => p.Id == PlaceId, ct);
            place.WanderMeetupCount.Should().Be(2);

            var meetup = await db.Meetups.AsNoTracking()
                .FirstAsync(m => m.Id == meetupId, ct);
            meetup.PromptSent.Should().BeFalse();
        }

        // -----------------------------------------------------------------------
        // Step 6: Recording FCM client final tally: 2 sends (invite push + accept push)
        // -----------------------------------------------------------------------
        App.FcmClient.Sends.Count.Should().Be(2);
    }

    // -----------------------------------------------------------------------
    // Local DTOs for deserialization (private to avoid polluting the namespace)
    // -----------------------------------------------------------------------

    private sealed record InviteDtoLocal(Guid Id);

    private sealed record AcceptInviteDtoLocal(Guid MeetupId);

    private sealed record SubmitReviewDtoLocal(ReviewItemLocal Review, RevieweeLocal Reviewee);

    private sealed record ReviewItemLocal(Guid Id, Guid MeetupId, Guid ReviewerId, Guid RevieweeId);

    private sealed record RevieweeLocal(Guid Id, int TrustScore, int MeetupCount);
}
