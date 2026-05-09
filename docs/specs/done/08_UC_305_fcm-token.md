{
  "$schema": ".claude/schemas/spec.v1.json",
  "uc_id": "UC-305",
  "slug": "fcm-token",
  "title": "Update the authenticated user's FCM device token (PATCH /users/me/fcm-token)",
  "actors": [
    "Authenticated WanderMeet user opening the mobile app — every app open posts the current FCM device token so push notifications can reach the active device."
  ],
  "preconditions": [
    "Caller has registered (User row exists for the JWT sub).",
    "Mobile client has a current FCM token from Firebase SDK.",
    "Phase 2 entities are applied. The User entity will gain a new nullable column `FcmToken` (string, max 512 chars). A migration is required for this column add.",
    "AuthorizationPolicies.UsersOnly is wired."
  ],
  "main_flow": [
    "Mobile app calls PATCH /api/v1/users/me/fcm-token with body { token: string } on every app open and after every Firebase token refresh.",
    "Endpoint resolves caller by JWT sub claim. If sub is missing → 401 Unauthorized.",
    "Endpoint validates input shape via Validator<UpdateFcmTokenRequest>: token NotEmpty (Validation.FcmTokenRequired), MaximumLength(ValidationConstants.FcmTokenMaxLength = 512) (Validation.FcmTokenTooLong). Validation failure → 400 with the error code(s) in the body via DontThrowIfValidationFails + if (ValidationFailed) guard, mirroring SendInviteEndpoint.",
    "Endpoint loads the tracked caller User row filtered by AzureAdB2CId == sub AND DeletedAt == null. If null → 404 with body code User.NotRegistered.",
    "Endpoint sets caller.FcmToken = req.Token (no trim — FCM tokens are well-formed and trimming would silently mutate them).",
    "Endpoint sets caller.LastActiveAt = TimeProvider.GetUtcNow() (consistent with every other write endpoint).",
    "Endpoint persists in a single SaveChangesAsync(ct).",
    "Endpoint returns 204 No Content. The client doesn't need any response body — the next push lookup will pick up the new token from DB.",
    "Subsequent app opens that re-post the same token are idempotent: the same string overwrites the same column; LastActiveAt still advances; SaveChangesAsync still issues an UPDATE (a single row affected, no behavioural change). The endpoint does not pre-check 'is this the same token?' — the cost of an extra SELECT is higher than a no-op UPDATE."
  ],
  "alternate_flows": [
    "Caller wants to clear the token (e.g. on logout / device sign-out) → out of scope for this UC. The endpoint requires a NotEmpty token. A future logout flow can null the column directly via a different endpoint, or by sending DELETE /users/me/fcm-token (not in this UC).",
    "Token comes from a previously-registered FCM project, was rotated, then sent to us late → we still overwrite. We do not validate the token shape against Firebase here; if it's bogus, the next push attempt simply fails (handled in UC-307 FCM push service).",
    "Two devices for the same user (e.g. phone + tablet) → currently overwrites. The roadmap field is singular `fcm_token` — multi-device fan-out is out of MVP. Adding a UserDevice table is a future UC."
  ],
  "acceptance_criteria": [
    "PATCH /api/v1/users/me/fcm-token with valid body and valid JWT returns 204 No Content with empty body.",
    "Persisted User row has FcmToken = req.Token (verbatim, no trim, no normalisation) and LastActiveAt = TimeProvider.GetUtcNow().",
    "Posting the same token twice in a row both succeed with 204; the second call still bumps LastActiveAt to the new TimeProvider value.",
    "Posting a different token replaces the previous value (last-write-wins).",
    "PATCH returns 400 + Validation.FcmTokenRequired when token is null, empty, or whitespace-only.",
    "PATCH returns 400 + Validation.FcmTokenTooLong when token length > ValidationConstants.FcmTokenMaxLength (512).",
    "PATCH returns 401 Unauthorized when the bearer token is missing or has no sub claim.",
    "PATCH returns 404 + User.NotRegistered when the JWT sub does not match any non-soft-deleted User row.",
    "Validator boundary: 512-char token passes; 513-char token fails with FcmTokenTooLong.",
    "Endpoint is internal sealed, inherits Endpoint<UpdateFcmTokenRequest> (no response generic — success is 204), declares DontCatchExceptions(), DontThrowIfValidationFails(), Policies(nameof(AuthorizationPolicies.UsersOnly)), and .RequireRateLimiting(RateLimitPolicies.GeneralApi) inside Description(...).",
    "Files placed at src/WanderMeet.Api/Features/Users/UpdateFcmToken/{UpdateFcmTokenEndpoint.cs, UpdateFcmTokenRequest.cs, UpdateFcmTokenValidator.cs}. The existing UsersFeatureConfiguration is reused — no new feature configuration in this slice.",
    "Summary block lists every status code returned (204, 400, 401, 404, 429).",
    "All async calls forward CancellationToken. TimeProvider injected via primary constructor; no DateTime.UtcNow.",
    "New error code constants added to src/WanderMeet.Shared/ErrorCodes.cs with XML <summary> docs under the existing Validation nested class: Validation.FcmTokenRequired = 'Validation.FcmTokenRequired', Validation.FcmTokenTooLong = 'Validation.FcmTokenTooLong'.",
    "New constant added to src/WanderMeet.Shared/ValidationConstants.cs: public const int FcmTokenMaxLength = 512.",
    "User entity gains a new property `public string? FcmToken { get; set; }`. UserConfiguration adds `builder.Property(x => x.FcmToken).HasMaxLength(ValidationConstants.FcmTokenMaxLength);` (nullable, no default, no index — column is not used as a filter).",
    "A new EF migration `AddFcmTokenToUser` adds the column. SQL: ALTER TABLE users ADD COLUMN fcm_token VARCHAR(512) NULL; (snake_case via existing naming convention).",
    "Integration tests in tests/WanderMeet.Api.IntegrationTests/Features/Users/UpdateFcmToken/UpdateFcmTokenEndpointTests.cs: 204 happy path with DB-state assertion, 204 idempotent re-post, 204 token replacement, 401 no-token, 404 caller not registered, 400 token-empty, 400 token-too-long. Use distinct X-Forwarded-For values in the 10.80.x.y range.",
    "Unit tests in tests/WanderMeet.Api.UnitTests/Features/Users/UpdateFcmToken/UpdateFcmTokenValidatorTests.cs cover the validator boundaries (null/empty/whitespace fails with FcmTokenRequired; 512 passes; 513 fails with FcmTokenTooLong)."
  ],
  "out_of_scope": [
    "Multi-device support: a User has at most one FcmToken in this MVP. Tablet + phone for the same user is out.",
    "Clearing the token (DELETE /users/me/fcm-token or NULL accept). Rotation only.",
    "Validating the token against Firebase. We trust the client; bad tokens fail downstream when push is attempted (UC-307).",
    "Push delivery itself. UC-305 is just the token-update path; sending the actual push is UC-307 (FCM push service).",
    "Rate-limiting differently from GeneralApi. The mobile app posts on every app open which can be many calls/day; the GeneralApi 100/min limit is sufficient.",
    "Auditing token changes (history table). Out of MVP."
  ],
  "non_functional": [
    "P95 < 150 ms. The endpoint does one tracked caller load + one UPDATE in a single SaveChanges. Existing AzureAdB2CId index on Users covers the lookup.",
    "All async calls forward CancellationToken. Tests use TestContext.Current.CancellationToken.",
    "FcmToken MUST NOT appear in any log output. The token is a per-device secret; logging it would be a privacy/security issue. Endpoint logs (if any) should reference only User.Id, not FcmToken.",
    "PII handling: FcmToken is per-device secret material. Stored in the existing users table; encrypted-at-rest is the database's default (Postgres TDE on Azure Flexible Server).",
    "Migration safety: nullable column add on a populated table is safe (no rewrite, no lock issue). EF will generate ALTER TABLE ADD COLUMN; review the SQL before applying."
  ]
}
