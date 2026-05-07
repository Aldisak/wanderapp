{
  "$schema": ".claude/schemas/spec.v1.json",
  "uc_id": "UC-205",
  "slug": "discovery",
  "title": "Nearby-user discovery feed with PostGIS spatial filter, cursor pagination, and arriving-soon list",
  "actors": [
    "Authenticated mobile app user with a registered profile"
  ],
  "preconditions": [
    "Caller has a User row in the database (registered via UC-201).",
    "Phase 2.1/2.2 entities and migrations are applied: Users.Location is `geography (Point, 4326)` with a GiST index, Cities.Location is the same, and the composite index `(city_id, is_open_today, last_active_at)` exists on Users.",
    "EFCore.NamingConventions and Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite are wired in `Program.cs` so spatial functions like `EF.Functions.IsWithinDistance` and the `Point` CLR type round-trip through Npgsql.",
    "DiscoveryRate-limit policy is registered in `Program.cs`."
  ],
  "main_flow": [
    "Mobile app calls GET /api/v1/discover?cityId={guid}&hangoutTagSlug={slug?}&limit={int?=20}&cursor={base64?}.",
    "Endpoint validates query params via FastEndpoints Validator: cityId non-empty Guid, limit in [1,50], hangoutTagSlug (if present) matches a known HangoutTagSlug enum value, cursor (if present) is well-formed base64 JSON of {LastActiveAt, TrustScore, Id, IsOpenToday}.",
    "Endpoint resolves caller's User by JWT sub claim. 401 if sub missing. 404 + User.NotRegistered if no User row.",
    "Endpoint loads the target City (only Id and Location). 404 + Discovery.CityNotFound if missing or soft-deleted.",
    "Endpoint queries Users with these predicates: `Location IS NOT NULL` and within ValidationConstants.DiscoveryRadiusMetres (50 km) of the City's Location via `EF.Functions.IsWithinDistance`; `IsOpenToday = true`; `LastActiveAt > now - ValidationConstants.DiscoveryActiveWindow (72 h)`; `Id != caller.Id`; `DeletedAt IS NULL`; if hangoutTagSlug supplied, the user has a UserHangoutTag whose HangoutTag.Slug matches; not present as either Sender or Receiver of a Pending Invite involving the caller (no double-discovery between caller and a user they have an open invite with, in either direction).",
    "Endpoint sorts by IsOpenToday DESC, TrustScore DESC, LastActiveAt DESC, Id DESC (Id is the deterministic tiebreaker so cursor pagination is stable).",
    "Endpoint applies cursor: when present, decode `{LastActiveAt, TrustScore, Id, IsOpenToday}` and add a keyset predicate that returns rows strictly after that key in the sort order.",
    "Endpoint takes `limit + 1` rows; if `limit + 1` returned, the last row is the page sentinel — drop it from the response and emit a nextCursor encoding the LAST returned row's keys; otherwise nextCursor = null.",
    "Endpoint returns 200 with `{ items: PublicUserDto[], nextCursor: string|null }`.",
    "Mobile app may also call GET /api/v1/discover/arriving?cityId={guid} to populate the empty-state 'arriving soon' cards.",
    "Endpoint validates cityId, resolves caller, loads city.",
    "Endpoint queries UserCities rows where CityId = target city, ArrivedAt is in the future and within the next 30 days, DepartedAt IS NULL, the owning User is not the caller, the User is not soft-deleted. Project to PublicUserDto + ArrivingAt.",
    "Endpoint returns 200 with `{ items: ArrivingUserDto[] }` (no pagination — list is small)."
  ],
  "alternate_flows": [
    "Caller has no User row (valid JWT, no profile) → 404 + User.NotRegistered on both endpoints.",
    "cityId references a non-existent or soft-deleted city → 404 + Discovery.CityNotFound.",
    "limit < 1 or > 50 → 400 + Validation.LimitOutOfRange.",
    "hangoutTagSlug supplied but does not match any HangoutTagSlug enum value → 400 + Validation.HangoutTagSlugInvalid.",
    "cursor supplied but cannot be base64-decoded into the expected JSON shape → 400 + Validation.CursorMalformed.",
    "No users match the filters → 200 with `{ items: [], nextCursor: null }`.",
    "Discovery rate limit exceeded → 429 + Retry-After header (handled by middleware)."
  ],
  "acceptance_criteria": [
    "GET /api/v1/discover with valid cityId returns 200 with up to 20 PublicUserDto rows by default.",
    "Spatial filter excludes users farther than 50 km from the city center; integration test seeds two users — one inside the radius, one outside — and asserts only the inside user is returned.",
    "Activity filter excludes users whose LastActiveAt is older than 72 hours; integration test seeds one stale and one fresh user and asserts only the fresh one is returned.",
    "is_open_today filter excludes users with IsOpenToday=false; integration test asserts.",
    "hangoutTagSlug filter restricts to users that have that specific tag; integration test seeds two users with different tags and asserts only the matching one is returned.",
    "Caller is never returned in their own discover feed.",
    "Soft-deleted users are never returned.",
    "Users with whom the caller has a pending invite (in either direction) are excluded; integration test seeds one such pair and asserts.",
    "Sort order is verified: integration test seeds three users with controlled (IsOpenToday, TrustScore, LastActiveAt) tuples and asserts the response order matches IsOpenToday DESC, TrustScore DESC, LastActiveAt DESC.",
    "Cursor pagination round-trips: integration test seeds 25 users matching all filters, calls limit=10 → asserts 10 items + nextCursor non-null, calls again with the cursor → asserts the next 10 items, calls a third time → asserts 5 items + nextCursor null. Page-2 must not include any rows from page 1 nor skip any rows.",
    "Malformed cursor → 400 + Validation.CursorMalformed; cursor pointing past the last row → 200 with empty items.",
    "GET /api/v1/discover/arriving with valid cityId returns 200 with users whose UserCities row has ArrivedAt in the future, within 30 days, DepartedAt null, CityId matches, owner not caller, owner not deleted.",
    "Both endpoints return 401 when no Bearer token is presented.",
    "Both endpoints return 404 + User.NotRegistered when the JWT sub maps to no User row.",
    "Both endpoints return 404 + Discovery.CityNotFound when cityId is unknown.",
    "Both endpoints declare `RequireRateLimiting(RateLimitPolicies.Discovery)` and `Policies(nameof(AuthorizationPolicies.UsersOnly))`.",
    "Endpoint uses PostGIS server-side spatial query (`EF.Functions.IsWithinDistance` against `geography (Point, 4326)` columns). The endpoint must NOT compute distance in application code.",
    "Endpoint uses AsNoTracking + projection (`.Select(u => new PublicUserDto(...))`) — no entity tracking on the discovery query.",
    "Validators are FastEndpoints `Validator<TRequest>`; endpoint files are `internal sealed`, with `DontCatchExceptions()` and `Send.*` pattern.",
    "Cursor encoding is opaque to clients: base64 over JSON of `{LastActiveAt, TrustScore, Id, IsOpenToday}`. Backend decodes deterministically; clients never construct cursors."
  ],
  "out_of_scope": [
    "Scoring beyond the simple sort tuple — no ML ranking, no personalisation. That is a Phase 4+ concern.",
    "Variable radius — a single global 50 km radius covers all cities for MVP.",
    "Multi-tag filter — only one hangoutTagSlug at a time. Comma-separated lists are out of scope.",
    "Reverse-geocoding the caller's coarse Location to infer cityId — caller passes cityId explicitly.",
    "Caching — every call hits the database. A second-tier cache (Redis or memory) is a later optimisation.",
    "Reciprocity surfacing — the spec already filters out users with a pending invite involving the caller; it does NOT promote users who have shown interest in the caller. That is a separate slice if/when it lands.",
    "Premium/feature-flag gating of discovery — Nomad Pass is Phase 6.",
    "Retry/backoff on PostGIS timeouts — the global exception handler covers DB faults."
  ],
  "non_functional": [
    "P95 < 400 ms for /discover with 10 000 users in the city. Verified by EXPLAIN showing the GiST + composite indexes are used (manual or `dotnet ef migrations script` review — out of automation but worth flagging).",
    "Spatial query is fully server-side: NEVER compute haversine in C#. Use `EF.Functions.IsWithinDistance(user.Location, city.Location, radiusMetres)`.",
    "Cursor JWT-style: base64 of canonical JSON; no signing for now (the cursor leaks no PII beyond the last seen row's stats). If signing is needed later, that is a separate slice.",
    "All async calls forward `CancellationToken`. Tests pass `TestContext.Current.CancellationToken`.",
    "Caller's `LastActiveAt` is NOT bumped by /discover calls (read-only feed). Last-active is bumped by mutation endpoints (Register/UpdateMe/etc.) — discover stays out of that path."
  ]
}
