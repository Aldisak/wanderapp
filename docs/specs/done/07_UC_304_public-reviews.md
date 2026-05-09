{
  "$schema": ".claude/schemas/spec.v1.json",
  "uc_id": "UC-304",
  "slug": "public-reviews",
  "title": "List public reviews about a user (GET /users/{id}/reviews)",
  "actors": [
    "Authenticated WanderMeet user viewing another user's profile (the viewer); the listed reviews are about a target user (the reviewee)."
  ],
  "preconditions": [
    "Caller has registered (User row exists for the JWT sub).",
    "Phase 2 + UC-302 entities are applied. The MeetupReview, Meetup, User, and UserPhoto tables exist with the FKs from the data model. Reviewer/Reviewee FK indexes are configured. No new migration is needed.",
    "ValidationConstants.ReviewTextMaxLength = 120 already exists.",
    "AuthorizationPolicies.UsersOnly is wired for authenticated reads."
  ],
  "main_flow": [
    "Mobile app calls GET /api/v1/users/{id:guid}/reviews on the profile screen.",
    "Endpoint resolves caller by JWT sub claim. If sub is missing → 401 Unauthorized.",
    "Endpoint verifies the target user exists via AsNoTracking AnyAsync filtered by Id == route id AND DeletedAt == null. If false → 404 Send.NotFoundAsync (matches GetByIdEndpoint pattern; no body code needed since the route id is itself the public lookup key).",
    "Endpoint queries MeetupReviews where RevieweeId == route id AND DidMeet == true. The DidMeet filter is intentional: only confirmed meetups produce reviews suitable for a public profile; non-met reviews are still in the trust-score formula but should not appear in the public list (they are predominantly negative signals from cancelled meetups).",
    "Endpoint orders by CreatedAt DESC and caps at Take(50) (same cap as GET /meetups/pending-review for symmetry; no cursor pagination for MVP — 50 most-recent reviews is plenty for a profile screen).",
    "Endpoint projects each row to a PublicReviewDto via .Select: { id, reviewer: ReviewerMiniDto (id, firstName, photoUrl), feltSafe, goodConvo, wouldMeetAgain, text, createdAt }. ReviewerMiniDto is a record. PhotoUrl projects the lowest-Order non-deleted UserPhoto.BlobUrl for the reviewer (mirrors the SendInvite/AcceptInvite/PendingReview projection idiom; soft-deleted photos are filtered out).",
    "Endpoint returns 200 with { items: PublicReviewDto[] } via Send.OkAsync. Empty items array if the target has no qualifying reviews (or has been reviewed but only with DidMeet=false). 200 + empty items is the correct shape — 404 is reserved for 'target user not found'."
  ],
  "alternate_flows": [
    "Target user exists but has zero MeetupReviews (newly registered) → 200 with items: [] (NOT 404).",
    "Target user has reviews but all DidMeet=false → 200 with items: [] (the public-list filter excludes them; the trust score still reflects them).",
    "Reviewer is soft-deleted at query time → still include the review; project the reviewer's first name + photo from their (soft-deleted) row. The review is about the target, not the reviewer; deleting a reviewer's account shouldn't suppress their past reviews from public profiles. (If the product later wants to anonymise these, that's a separate UC.)",
    "Target user is soft-deleted (DeletedAt != null) → 404 Send.NotFoundAsync. Mirrors GetByIdEndpoint behaviour.",
    "Caller views their own profile (route id == caller.Id) → still 200 with the same shape. No special-casing — the caller's own reviews about them are public to them too."
  ],
  "acceptance_criteria": [
    "GET /api/v1/users/{id:guid}/reviews returns 200 with { items: PublicReviewDto[] } when the target exists and has DidMeet=true reviews.",
    "Items are ordered by CreatedAt DESC.",
    "Items are capped at 50.",
    "DidMeet=false reviews are excluded from items even though they exist in the table for the same RevieweeId. Verified by an integration test seeding one DidMeet=true and one DidMeet=false review for the same reviewee and asserting items.Count == 1.",
    "PublicReviewDto has shape (Guid Id, ReviewerMiniDto Reviewer, bool FeltSafe, bool GoodConvo, bool WouldMeetAgain, string? Text, DateTimeOffset CreatedAt). DidMeet is intentionally NOT in the DTO — it is constant true for every item by virtue of the filter.",
    "ReviewerMiniDto has shape (Guid Id, string FirstName, string? PhotoUrl). It lives at Features/Users/PublicReviews/Shared/ReviewerMiniDto.cs (NOT in Features/Meetups/Shared/, NOT in Features/Invites/Shared/ — no cross-feature references).",
    "PhotoUrl projects MIN(Order) UserPhoto.BlobUrl filtered by DeletedAt == null. Soft-deleted photos are excluded.",
    "GET returns 200 with items: [] when the target has no qualifying reviews (target exists but reviews are absent or all DidMeet=false). 404 is NEVER returned in this case.",
    "GET returns 401 Unauthorized when the bearer token is missing or has no sub claim.",
    "GET returns 404 Send.NotFoundAsync (no body code) when the route id does not match a non-deleted User row. Mirrors GetByIdEndpoint.",
    "Soft-deleted reviewers (Reviewer.DeletedAt != null) still appear in the items list — their review survives their account deletion.",
    "The endpoint sits at src/WanderMeet.Api/Features/Users/PublicReviews/ListPublicReviews/ListPublicReviewsEndpoint.cs with companion ListPublicReviewsRequest.cs (route-bound id) and ListPublicReviewsResponse.cs. The PublicReviewDto and ReviewerMiniDto live under Features/Users/PublicReviews/Shared/. The existing UsersFeatureConfiguration is reused — no new feature configuration in this slice (Users is already an established slice).",
    "Endpoint is internal sealed, inherits Endpoint<ListPublicReviewsRequest, ListPublicReviewsResponse>, declares DontCatchExceptions(), Policies(nameof(AuthorizationPolicies.UsersOnly)), .RequireRateLimiting(RateLimitPolicies.GeneralApi). No validator (route-bound Guid is already type-checked by FastEndpoints; no body input).",
    "Summary block lists every status code returned (200, 401, 404, 429).",
    "All read paths use AsNoTracking + .Select projections. Single SQL query for the items list — no N+1 across reviewers or photos. Use .Where(...).Select(...).Take(50).ToListAsync. Verify the EF-generated SQL is one statement (one JOIN to Users for the reviewer mini, one correlated SELECT for the PhotoUrl).",
    "All async calls forward CancellationToken (parameter named ct, last position).",
    "TimeProvider is NOT needed in this slice — the read does not touch any timestamp setting (CreatedAt is read, not written). Don't inject it.",
    "No new error codes. No migration. No DbSet changes. No DI registration.",
    "Integration tests in tests/WanderMeet.Api.IntegrationTests/Features/Users/PublicReviews/ListPublicReviewsEndpointTests.cs cover: 200 with mixed DidMeet=true/false (only true items returned), 200 ordering by CreatedAt DESC, 200 with empty items list when target has no reviews, 200 with empty items when target's only reviews are DidMeet=false, 200 cap at 50 (seed 51), 200 reviewer-soft-deleted still listed, 200 photo-url projection (lowest non-deleted order), 200 photo-url null when reviewer has no photos, 401 no-bearer, 404 unknown user, 404 soft-deleted target. Use distinct X-Forwarded-For values in the 10.70.x.y range (mirrors UC-303's 10.60.x.y allocation; UC-301 used 10.30/10.40, UC-302 used 10.40/10.50, UC-303 reserved 10.60).",
    "No reference from Features/Users/PublicReviews/* to Features/Meetups/* or Features/Invites/*. Cross-feature shared types are duplicated locally (the new ReviewerMiniDto is its own record, not Features/Meetups/Shared/MeetupUserMiniDto)."
  ],
  "out_of_scope": [
    "Cursor pagination. The 50-item cap is sufficient for the profile-screen MVP.",
    "Filtering / sorting options (most recent, most positive, etc.). The fixed CreatedAt DESC order is the only mode.",
    "Anonymising reviews from soft-deleted reviewers. They appear with their original FirstName and PhotoUrl until product decides otherwise.",
    "Showing aggregate stats (counts by emoji-badge) on this endpoint — the trust score on the existing GET /users/{id} already covers that.",
    "Editing or deleting reviews from the public list. Reviews remain immutable per UC-302.",
    "Reporting a review (vs. a user). UC-303 is intake-only against users, not against review rows.",
    "Author-side filtering ('show only reviews I left about this user') — out of MVP."
  ],
  "non_functional": [
    "P95 < 250 ms. Single SQL statement: filter by RevieweeId + DidMeet, JOIN Users for reviewer mini, correlated subquery for PhotoUrl, ORDER + LIMIT 50. Existing FK index on RevieweeId covers the lookup; ORDER BY CreatedAt is a sequential scan on the (small) result subset and acceptable for MVP.",
    "All async calls forward CancellationToken. Tests use TestContext.Current.CancellationToken.",
    "No new entities. No migration. No DbSet additions.",
    "Public-data leakage: only the reviewer's FirstName and PhotoUrl are exposed. No email, last name, AzureAdB2CId, or any other identifier. PublicReviewDto is the contract — the projection MUST NOT widen it without a separate UC.",
    "The default 50-item cap MUST be enforced server-side. Do not accept a client-supplied limit; if the product wants pagination later, that's a separate UC."
  ]
}
