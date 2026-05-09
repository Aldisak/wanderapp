{
  "$schema": ".claude/schemas/spec.v1.json",
  "uc_id": "UC-302",
  "slug": "meetups-reviews",
  "title": "Pending-review list and post-meetup review submission with trust-score recalculation",
  "actors": [
    "Authenticated WanderMeet user who participated in a Meetup (either UserA or UserB)"
  ],
  "preconditions": [
    "Caller has registered (User row exists for the JWT sub).",
    "Phase 2 entities + migrations are applied. Meetup, MeetupReview, User, Place tables exist with the FKs from the data model. MeetupReview has a unique (MeetupId, ReviewerId) index so the same caller cannot review the same meetup twice at the DB level.",
    "UC-301 (Invites) is shipped: Meetup rows are created on PATCH /invites/{id}/accept.",
    "ValidationConstants.ReviewTextMaxLength = 120 already exists.",
    "ValidationConstants.TrustScoreMin = 0 and TrustScoreMax = 100 already exist."
  ],
  "main_flow": [
    "Mobile app calls GET /api/v1/meetups/pending-review on app open.",
    "Endpoint resolves caller by JWT sub. 401 if sub missing; 404 + User.NotRegistered if no User row.",
    "Endpoint queries Meetups where the caller is UserA or UserB AND there is no MeetupReview by the caller for that Meetup. Sorted by MetAt DESC. Capped at Take(50).",
    "Endpoint projects each row to a PendingReviewDto: { meetupId, otherUser: UserMiniDto (the OTHER participant — not caller), place: PlaceMiniDto, metAt }. The 'other user' is computed: if Meetup.UserAId == caller.Id then otherUser = UserB else otherUser = UserA.",
    "Endpoint returns 200 with { items: PendingReviewDto[] }.",
    "Mobile app calls POST /api/v1/meetups/{id}/review with body { didMeet, feltSafe, goodConvo, wouldMeetAgain, text? }.",
    "Endpoint resolves caller by JWT sub.",
    "Endpoint loads the Meetup tracked, projecting Id + UserAId + UserBId + PlaceId. 404 if not found OR caller is neither UserA nor UserB (Meetup.NotFound — treat foreign meetups as not found).",
    "Endpoint determines the reviewee: if caller.Id == UserAId then reviewee = UserB, else reviewee = UserA.",
    "Endpoint checks for an existing MeetupReview where MeetupId == meetup.Id AND ReviewerId == caller.Id. If found → 409 + Meetup.AlreadyReviewed.",
    "Endpoint creates the MeetupReview row with all fields populated, ReviewerId = caller.Id, RevieweeId = reviewee.Id, CreatedAt = now. Adds to context.",
    "Endpoint recomputes the reviewee's trust score from the database in a single aggregation query: count reviews where Reviewee = reviewee.Id, summing DidMeet, FeltSafe, GoodConvo, WouldMeetAgain. The new MeetupReview must be reflected — perform the recomputation AFTER SaveChangesAsync, OR include the in-memory new review in the LINQ aggregation. Designer picks the cleanest approach.",
    "Endpoint applies the formula: base_score = (meetup_count * 6) + (felt_safe_count * 4) + (would_meet_again_count * 3) + (good_convo_count * 2); reviewee.TrustScore = clamp(base_score, 0, 100); reviewee.MeetupCount = meetup_count (denormalised count of reviews-about-them where DidMeet=true).",
    "If req.DidMeet == true, increment the Meetup.Place's WanderMeetupCount by 1 (single Place.WanderMeetupCount += 1). The increment fires once per did-meet review. If both participants submit did-meet=true reviews, Place.WanderMeetupCount goes up by 2 — that is a known and accepted denormalisation choice (mirrors the 'two reviews per meetup' shape).",
    "Endpoint persists Review + reviewee.TrustScore + reviewee.MeetupCount + place.WanderMeetupCount in a single SaveChangesAsync. Updates caller.LastActiveAt = now.",
    "Endpoint returns 200 with { review: ReviewDto, reviewee: { id, trustScore, meetupCount } } so the client can show the updated trust badge without re-fetching."
  ],
  "alternate_flows": [
    "Caller is not UserA/UserB → 404 + Meetup.NotFound (do not 403 — leaks the meetup id).",
    "Caller has already submitted a review for this meetup → 409 + Meetup.AlreadyReviewed.",
    "Soft-deleted reviewee at the time of review submission → review still saves; trust score still recomputed (the soft-delete is the user's own state, not a guard against being reviewed).",
    "DidMeet=false → MeetupReview persisted but Place.WanderMeetupCount NOT incremented; reviewee.MeetupCount is recomputed from all reviews including this one (DidMeet=false → does not contribute to meetup_count).",
    "text omitted or null → review still valid; only the bool fields are required."
  ],
  "acceptance_criteria": [
    "GET /api/v1/meetups/pending-review returns 200 with the caller's not-yet-reviewed meetups, ordered MetAt DESC, capped at 50.",
    "Pending-review list excludes meetups the caller has already reviewed (covered by integration test seeding 2 meetups + 1 existing review).",
    "Pending-review otherUser correctly identifies the non-caller participant in both directions (caller as UserA or UserB).",
    "GET /pending-review returns 401 (no token) and 404 + User.NotRegistered (no User row).",
    "POST /api/v1/meetups/{id}/review returns 200 with the new ReviewDto + a reviewee summary including the recomputed trustScore and meetupCount.",
    "Persisted MeetupReview row has all fields populated, ReviewerId = caller, RevieweeId = the other participant, CreatedAt = TimeProvider now.",
    "Trust score is recomputed on the reviewee per the documented formula and CLAMPed to [0, 100]. Verified by an integration test: seed a meetup, submit a review with feltSafe=true, goodConvo=true, wouldMeetAgain=true, didMeet=true → reviewee.TrustScore == clamp(1*6 + 1*4 + 1*3 + 1*2, 0, 100) == 15 (assuming reviewee had no prior reviews).",
    "Trust score CLAMP boundary: seed reviewee with 50 prior did-meet reviews all positive → recompute should return 100, not 50*15.",
    "POST returns 400 + Validation.ReviewTextTooLong when text > 120 chars (validator).",
    "POST returns 404 + Meetup.NotFound when meetup id is unknown.",
    "POST returns 404 + Meetup.NotFound when caller is not UserA/UserB (treat foreign meetups as not found).",
    "POST returns 409 + Meetup.AlreadyReviewed when caller has already reviewed this meetup.",
    "POST with didMeet=true increments Place.WanderMeetupCount by exactly 1; integration test asserts before/after delta.",
    "POST with didMeet=false does NOT increment Place.WanderMeetupCount; integration test asserts.",
    "Both endpoints declare Policies(nameof(AuthorizationPolicies.UsersOnly)) and RequireRateLimiting(RateLimitPolicies.GeneralApi).",
    "POST has a FastEndpoints Validator<TRequest>; the GET has none (no input shape to validate beyond the route).",
    "Endpoint files are internal sealed, inherit Endpoint<TReq, TRes>, declare DontCatchExceptions(), use Send.* pattern, sit under Features/Meetups/PendingReview/ and Features/Meetups/SubmitReview/ with a MeetupsFeatureConfiguration.",
    "All read paths use AsNoTracking + projection. Mutations load tracked.",
    "Integration tests cover: GET pending happy path + filter (already-reviewed excluded), GET 401 + 404 NotRegistered, POST 200 with the trust-score-recalc assertion, POST 200 with CLAMP boundary, POST didMeet=true Place increment, POST didMeet=false no Place increment, POST 404 unknown, POST 404 foreign meetup, POST 409 already reviewed, POST 400 text-too-long. Use distinct X-Forwarded-For per test.",
    "Unit tests: SubmitReviewValidatorTests for the text-length boundary cases; TrustScoreCalculatorTests if a separate calculator helper is extracted (designer's call)."
  ],
  "out_of_scope": [
    "Editing or deleting a review after submission. Reviews are immutable.",
    "Public review listing on a user's profile (`GET /users/{id}/reviews` from the roadmap) — separate slice; this UC delivers the submission + recalc, not the public viewing.",
    "Background review-prompt push notification (3h after meetup) — UC-304 / Phase 3b territory.",
    "Notifying the reviewee that they've been reviewed — Phase 3b realtime/push concerns.",
    "Anti-abuse: detecting reviewer-bombing or coordinated downvotes — separate slice.",
    "Trust-score history / audit trail — only the current value is stored.",
    "Recomputing trust scores in bulk for migrations — out of MVP."
  ],
  "non_functional": [
    "P95 < 400 ms for POST /review including the recompute. The recompute is a single GROUP BY on MeetupReviews — already covered by the existing index on (MeetupId, ReviewerId) plus the FK index on Reviewee.",
    "P95 < 300 ms for GET /pending-review. The query joins Meetups against MeetupReviews via NOT EXISTS — must be a single SQL statement; verify EF emits one query.",
    "Trust score MUST be recomputed server-side. NEVER trust a client-supplied trustScore field. The roadmap rule is non-negotiable.",
    "All async calls forward CancellationToken. Tests use TestContext.Current.CancellationToken.",
    "No new entities. No migration. Use the existing Meetup, MeetupReview, User, Place, UserPhoto tables.",
    "Idempotency: the (MeetupId, ReviewerId) unique index is the ultimate guard against double-submit. The endpoint pre-check is a clean 409 path; if the unique-violation race fires, the resulting DbUpdateException is allowed to bubble to the global handler (500). Acceptable per the existing pattern in UC-201 Register."
  ]
}
