{
  "$schema": ".claude/schemas/spec.v1.json",
  "uc_id": "UC-309",
  "slug": "invite-lifecycle-e2e",
  "title": "End-to-end invite lifecycle integration test (send → accept → review → trust-score updated)",
  "actors": [
    "Two authenticated WanderMeet users — sender and receiver — driving a single invite from creation through review."
  ],
  "preconditions": [
    "UC-301 Invites + UC-302 Meetups + UC-306 SignalR + UC-307 FCM are all shipped (they are).",
    "Test infra: WanderMeetApiFactory swaps in RecordingInviteNotifier and RecordingFcmClient via the Composite registration. App.FakeTimeProvider controls 'now'."
  ],
  "main_flow": [
    "Test class lives at tests/WanderMeet.Api.IntegrationTests/E2E/InviteLifecycleE2ETests.cs. Single [Collection(TestConstants.Collections.PipelineTest)] integration class deriving IntegrationTestBase.",
    "Test seeds two users (Alice = sender, Bob = receiver) in the same city, with Bob having a non-null FcmToken. Seeds one HangoutTag (coffee) + one Place in that city.",
    "Step 1: Alice POSTs /api/v1/invites with body { receiverId: bob.Id, hangoutTagId, placeId, senderIsThere: false }. Assert 201, capture inviteId from the response. Assert RecordingInviteNotifier.Sent contains the invite (SignalR fan-out fired). Assert App.FcmClient.Sends contains an FCM push to Bob's token with the standard 'Coffee at {placeName}?' title.",
    "Step 2: Bob PATCHes /api/v1/invites/{id}/accept. Assert 200, capture meetupId from the response. Assert RecordingInviteNotifier.Accepted contains the (invite, meetupId) tuple. Assert App.FcmClient.Sends contains an FCM push to Alice's token with the 'See you there!' title.",
    "Step 3: Both Alice and Bob initially have TrustScore=0 + MeetupCount=0. Assert via DB read.",
    "Step 4: Alice POSTs /api/v1/meetups/{meetupId}/review with { didMeet: true, feltSafe: true, goodConvo: true, wouldMeetAgain: true, text: 'Great chat!' }. Assert 200. Assert response body's reviewee.trustScore == 15 (formula: 1*6 + 1*4 + 1*3 + 1*2) and reviewee.meetupCount == 1.",
    "Step 5: Bob POSTs the symmetric review for the same meetup. Assert 200. Assert Alice's trust-score is now 15.",
    "Step 6: DB-side assertion — both users now have TrustScore=15, MeetupCount=1. Place.WanderMeetupCount has been incremented by 2 (one increment per did-meet review). The Meetup row has PromptSent=false (no Hangfire run during the test).",
    "Step 7: assert RecordingInviteNotifier captured exactly 1 Sent event + 1 Accepted event + 0 Declined + 0 Expired. RecordingFcmClient.Sends has exactly 2 entries (the standard invite push + the accepted push)."
  ],
  "alternate_flows": [
    "Notifier or FCM throws → not part of THIS test (already covered by per-feature tests). The E2E test asserts the happy-path of the full chain end-to-end."
  ],
  "acceptance_criteria": [
    "New file tests/WanderMeet.Api.IntegrationTests/E2E/InviteLifecycleE2ETests.cs.",
    "Single [Fact] method named EndToEnd_SendAcceptReview_TrustScoreUpdated.",
    "[Collection(TestConstants.Collections.PipelineTest)] on the class. IntegrationTestBase parent. ResetDatabaseAsync first in SetupAsync (inherited).",
    "All async calls forward TestContext.Current.CancellationToken.",
    "Distinct X-Forwarded-For per HTTP call within the test (10.120.x.y range).",
    "Test resets RecordingInviteNotifier + RecordingFcmClient (already done in IntegrationTestBase.SetupAsync).",
    "Seed code uses deterministic Guids (00000000-...) for Alice + Bob + city + place + tag.",
    "DB assertions use AsNoTracking().",
    "Test must pass on `dotnet test --filter \"FullyQualifiedName~InviteLifecycleE2ETests\"` and the full suite must remain green at 381+.",
    "If the trust-score formula in the response body changes shape, the test breaks loudly — the assertion is on the exact value 15, not a >0 sanity check. Use ValidationConstants.TrustScoreMax for the boundary upper-bound is OUT (no boundary cases here — single happy path).",
    "Verifies both notifier paths (SignalR via RecordingInviteNotifier; FCM via RecordingFcmClient) are wired correctly across the full lifecycle."
  ],
  "out_of_scope": [
    "Hangfire job runs (review-prompt push 3h after accept). Out of test scope; handled by UC-308 unit tests.",
    "Decline path. UC-301 already covers it.",
    "Concurrent reviewers race. UC-302 implementation note acknowledges this.",
    "Multi-meetup history per user. Single-meetup happy path only."
  ],
  "non_functional": [
    "Test runtime < 15 s. The full chain is 4 HTTP calls + a few DB reads.",
    "All async calls forward CancellationToken.",
    "No new entities, no migration, no DI changes."
  ]
}
