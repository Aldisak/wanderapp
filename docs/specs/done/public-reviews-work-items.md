# UC-304 Public Reviews — Work Items

Single-WI vertical slice for `GET /api/v1/users/{id:guid}/reviews`. The endpoint lives under the existing `Users` feature folder and reuses `UsersFeatureConfiguration`.

## Assumptions

- The `MeetupReview` entity, its `RevieweeId` index, and the `Text` max-length already exist (UC-302). No migration in this slice.
- `User.DeletedAt` soft-delete semantics match `GetByIdEndpoint` — a soft-deleted target returns 404; soft-deleted reviewers are still listed (the review survives the reviewer's account deletion).
- `UserPhoto.DeletedAt`/`UserPhoto.Order` semantics already used by `Meetups/PendingReview`, `Invites/SendInvite`, `Invites/AcceptInvite` projections — the lowest non-deleted Order wins.
- The route id is the public lookup key. There is no separate handle/slug → no extra existence check beyond `AnyAsync(u => u.Id == route.Id && u.DeletedAt == null)`.
- The 50-item cap is fixed and server-side (no `?limit=` query param). Pagination is explicitly out of scope.
- `DidMeet` is intentionally absent from `PublicReviewDto`: every item in the public list has `DidMeet == true` by virtue of the filter, so emitting the column would be dead weight.
- New shared types (`PublicReviewDto`, `ReviewerMiniDto`) are duplicated locally under `Features/Users/PublicReviews/Shared/`. They are NOT reused from `Features/Meetups/Shared/MeetupUserMiniDto` or `Features/Invites/Shared/InviteUserMiniDto` (no cross-feature references — `rules/architecture.md#no-horizontal-layers`).
- `TimeProvider` is not injected — the slice only reads `CreatedAt`, never writes a timestamp.
- Integration test IP allocation: prior slices used 10.30/10.40 (UC-301), 10.40/10.50 (UC-302), 10.60 (UC-303). This UC reserves `10.70.x.y` for `X-Forwarded-For` to keep the rate-limit quota isolated per test (`CLAUDE.md → Rate-limit test isolation`).

## Dependency Graph

```mermaid
graph TD
    WI1[WI-1: GET /users/{id}/reviews]
```

## WI-1: GET /users/{id}/reviews — list public reviews (DidMeet=true) for a target user

### Required Reads

- `docs/specs/in-progress/07_UC_304_public-reviews.md`
- `src/WanderMeet.Api/Features/Users/UsersFeatureConfiguration.cs`
- `src/WanderMeet.Api/Features/Users/GetById/GetByIdEndpoint.cs` — copy `Configure()` shape almost verbatim
- `src/WanderMeet.Api/Features/Users/GetById/GetByIdRequest.cs`
- `src/WanderMeet.Api/Features/Meetups/PendingReview/PendingReviewEndpoint.cs` — closest read-with-projection pattern (PhotoUrl via correlated subquery, `Take(50)` cap, `AsNoTracking + .Select`)
- `src/WanderMeet.Api/Features/Meetups/PendingReview/ListPendingReviewsResponse.cs` — response wrapper shape `{ items: T[] }`
- `src/WanderMeet.Api/Features/Meetups/Shared/MeetupUserMiniDto.cs` — shape reference only; do NOT import
- `src/WanderMeet.Api/Features/Meetups/SubmitReview/SubmitReviewEndpoint.cs` — write side that produces these rows
- `src/WanderMeet.Api/Database/Entities/MeetupReview.cs`
- `src/WanderMeet.Api/Database/Entities/User.cs`
- `src/WanderMeet.Api/Database/Entities/UserPhoto.cs`
- `src/WanderMeet.Api/Infrastructure/EntityFramework/Configurations/MeetupReviewConfiguration.cs` — confirms `RevieweeId` index already exists
- `src/WanderMeet.Api/Infrastructure/EntityFramework/WanderMeetDbContext.cs`
- `src/WanderMeet.Api/Authorization/AuthorizationPolicies.cs`
- `src/WanderMeet.Api/Common/RateLimitPolicies.cs`
- `tests/WanderMeet.Api.IntegrationTests/Features/Meetups/PendingReview/PendingReviewEndpointTests.cs` — closest test patterns for seeding users + meetups + reviews
- `tests/WanderMeet.Api.IntegrationTests/Features/Users/GetById/`
- `tests/WanderMeet.Api.IntegrationTests/Infrastructure/IntegrationTestFixture.cs`
- `tests/WanderMeet.Api.IntegrationTests/Infrastructure/IntegrationTestBase.cs`
- `tests/WanderMeet.Api.IntegrationTests/Infrastructure/TestConstants.cs`

### Deliverables

Production code (`src/WanderMeet.Api/Features/Users/PublicReviews/`):

- `ListPublicReviews/ListPublicReviewsEndpoint.cs` — `internal sealed`, `Endpoint<ListPublicReviewsRequest, ListPublicReviewsResponse>`, primary ctor injects `WanderMeetDbContext`. `Configure()`: `Get("users/{id:guid}/reviews")`, `Description(b => b.WithName(nameof(ListPublicReviewsEndpoint)).WithTags(_featureConfiguration.Info.Name).RequireRateLimiting(RateLimitPolicies.GeneralApi))`, `DontCatchExceptions()`, `Policies(nameof(AuthorizationPolicies.UsersOnly))`, `Summary` lists 200/401/404/429. `HandleAsync`: (1) `targetExists = await dbContext.Users.AsNoTracking().AnyAsync(u => u.Id == req.Id && u.DeletedAt == null, ct)`; (2) if `!targetExists` → `Send.NotFoundAsync(ct); return;`; (3) `var items = await dbContext.MeetupReviews.AsNoTracking().Where(r => r.RevieweeId == req.Id && r.DidMeet).OrderByDescending(r => r.CreatedAt).Take(50).Select(r => new PublicReviewDto(r.Id, new ReviewerMiniDto(r.Reviewer!.Id, r.Reviewer.FirstName, r.Reviewer.Photos.Where(p => p.DeletedAt == null).OrderBy(p => p.Order).Select(p => p.BlobUrl).FirstOrDefault()), r.FeltSafe, r.GoodConvo, r.WouldMeetAgain, r.Text, r.CreatedAt)).ToListAsync(ct);`; (4) `await Send.OkAsync(new ListPublicReviewsResponse(items), ct);`. Field on the class body: `private readonly UsersFeatureConfiguration _featureConfiguration = new();`.
- `ListPublicReviews/ListPublicReviewsRequest.cs` — `public record ListPublicReviewsRequest { public Guid Id { get; init; } }` with `<summary>` xmldoc.
- `ListPublicReviews/ListPublicReviewsResponse.cs` — `public record ListPublicReviewsResponse(IReadOnlyList<PublicReviewDto> Items)` with `<summary>` xmldoc.
- `Shared/PublicReviewDto.cs` — `public record PublicReviewDto(Guid Id, ReviewerMiniDto Reviewer, bool FeltSafe, bool GoodConvo, bool WouldMeetAgain, string? Text, DateTimeOffset CreatedAt)`.
- `Shared/ReviewerMiniDto.cs` — `public record ReviewerMiniDto(Guid Id, string FirstName, string? PhotoUrl)`. Local to this slice — NOT a reference to any sibling feature DTO.

Test code:

- `tests/WanderMeet.Api.IntegrationTests/Features/Users/PublicReviews/ListPublicReviewsEndpointTests.cs` — `[Collection(TestConstants.Collections.PipelineTest)]`, extends `IntegrationTestBase`, `SetupAsync` first line is `await app.ResetDatabaseAsync()`, every async call forwards `TestContext.Current.CancellationToken`, distinct `X-Forwarded-For` per test in the `10.70.x.y` range. Covers happy path + ordering + cap + photo projection + soft-deleted reviewer + caller-views-self.
- `tests/WanderMeet.Api.UnitTests/Features/Users/PublicReviews/ListPublicReviewsEndpointTests.cs` — 401 / 404-unknown / 404-soft-deleted-target. (Per `skills/gc-tdd/references/testing-conventions.md#test-ordering`: 401/403/404 belong in the unit suite; happy path + DB-shape assertions belong in integration.)

What is NOT in scope: no validator (route-bound `Guid` is type-checked by FastEndpoints; no body), no new `UsersFeatureConfiguration` (the existing one is reused), no migration, no `DbSet` change, no DI registration, no `TimeProvider` injection.

### Error Paths

| Status | Trigger | Send call |
|--------|---------|-----------|
| 401 | Bearer token missing or has no `sub` claim | Handled by `AuthorizationPolicies.UsersOnly` before `HandleAsync` runs |
| 404 | Route id does not match any `User` row, OR matches a soft-deleted user (`DeletedAt != null`) | `Send.NotFoundAsync(ct)` (no body code) |
| 429 | Rate limit exceeded | Handled by `RateLimitPolicies.GeneralApi` |

200 with `items: []` is the correct shape when:
- the target exists but has no reviews, or
- the target exists but every review has `DidMeet == false`.

404 is **never** returned for "target exists but has no qualifying reviews".

### Tests

Integration (`tests/WanderMeet.Api.IntegrationTests/Features/Users/PublicReviews/ListPublicReviewsEndpointTests.cs`):

- `HandleAsync_TargetHasMixedDidMeet_ReturnsOnlyDidMeetTrueItems` — seed two reviews for the same `RevieweeId`, one `DidMeet=true` and one `DidMeet=false`; assert `items.Count == 1` and the `DidMeet=false` reviewer's id is absent.
- `HandleAsync_MultipleReviews_OrdersByCreatedAtDescending` — seed three reviews with distinct `CreatedAt`; assert the response order is monotonically decreasing.
- `HandleAsync_TargetHasNoReviews_Returns200WithEmptyItems` — seed target with zero reviews; assert 200 + empty `items`.
- `HandleAsync_AllReviewsDidMeetFalse_Returns200WithEmptyItems` — seed only `DidMeet=false` reviews; assert 200 + empty `items` (NOT 404).
- `HandleAsync_MoreThan50Reviews_CapsAt50` — seed 51 `DidMeet=true` reviews with strictly increasing `CreatedAt`; assert `items.Count == 50` and the oldest one is excluded.
- `HandleAsync_ReviewerSoftDeleted_StillIncludedInItems` — seed a reviewer with `DeletedAt != null`; assert their review still appears with their original `FirstName` and `PhotoUrl`.
- `HandleAsync_ReviewerHasMultiplePhotos_ProjectsLowestOrderNonDeleted` — seed reviewer with photos `Order=0` (deleted), `Order=1` (active), `Order=2` (active); assert `PhotoUrl` equals the `Order=1` `BlobUrl`.
- `HandleAsync_ReviewerHasNoPhotos_PhotoUrlIsNull` — seed reviewer with zero photos; assert `items[0].Reviewer.PhotoUrl is null`.
- `HandleAsync_CallerViewsOwnProfile_Returns200WithSameShape` — route id == caller.Id; assert 200 and the response shape matches the third-party-viewer case.

Unit (`tests/WanderMeet.Api.UnitTests/Features/Users/PublicReviews/ListPublicReviewsEndpointTests.cs`):

- `HandleAsync_NoBearerToken_Returns401` — call without `Authorization` header.
- `HandleAsync_UnknownUserId_Returns404` — call with a `Guid` that does not match any `User` row.
- `HandleAsync_TargetSoftDeleted_Returns404` — seed a `User` with `DeletedAt != null`.

Every async call forwards `TestContext.Current.CancellationToken` (`skills/gc-tdd/references/testing-conventions.md#cancellation-token`). Use distinct `X-Forwarded-For` IPs in `10.70.x.y` to avoid rate-limit quota leakage (`CLAUDE.md → Rate-limit test isolation`). `ResetDatabaseAsync` is the first line of `SetupAsync` (`skills/gc-tdd/references/testing-conventions.md#reset-database-first`).

### Verification

```
dotnet test --filter "FullyQualifiedName~PublicReviews"
```

Equivalent structured form: `{ "tool": "dotnet-test", "filter": "PublicReviews" }`. Runs both the new integration class and the new unit class; nothing else in the suite matches the `PublicReviews` fragment.
