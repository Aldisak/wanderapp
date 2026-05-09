{
  "$schema": ".claude/schemas/spec.v1.json",
  "uc_id": "UC-303",
  "slug": "reports",
  "title": "Submit a user report (intake-only) with daily-throttle and self/missing-target guards",
  "actors": [
    "Authenticated WanderMeet user (the reporter) reporting another WanderMeet user (the target)"
  ],
  "preconditions": [
    "Caller has registered (User row exists for the JWT sub).",
    "Phase 2 entities + migrations are applied. The Report entity, ReportConfiguration, and DbSet<Report> Reports already exist in src/WanderMeet.Api with: ReporterId (FK Users), ReportedId (FK Users), Reason (string, max 300 chars, required), ReviewedAt (DateTimeOffset? nullable), plus AuditableEntity fields (Id Guid, CreatedAt DateTimeOffset, UpdatedAt DateTimeOffset?). FK indexes on ReporterId and ReportedId are configured. No new migration is needed.",
    "ValidationConstants.ReportReasonMaxLength = 300 already exists.",
    "RateLimitPolicies.Reports (5/day per authenticated user) is already registered in Program.cs and partitions on the JWT sub."
  ],
  "main_flow": [
    "Mobile app calls POST /api/v1/reports with body { reportedUserId: Guid, reason: string }.",
    "Endpoint validates input shape via Validator<SubmitReportRequest>: reportedUserId NotEmpty (Validation.ReportedUserIdRequired), reason NotEmpty (Validation.ReportReasonRequired), reason MaximumLength(ValidationConstants.ReportReasonMaxLength) (Validation.ReportReasonTooLong). Validation failure → 400 with the error code(s) in the body via DontThrowIfValidationFails + if (ValidationFailed) guard, mirroring SendInviteEndpoint.",
    "Endpoint resolves caller by JWT sub claim. If sub is missing → 401 Unauthorized.",
    "Endpoint loads the tracked caller User row filtered by AzureAdB2CId == sub AND DeletedAt == null. If null → 404 with body code User.NotRegistered (mirrors SendInviteEndpoint).",
    "Endpoint guards against self-report: if req.ReportedUserId == caller.Id → 400 with body code Validation.CannotReportSelf. Cheap guard, no DB lookup; mirrors the SelfInviteForbidden pattern.",
    "Endpoint verifies the reported user exists via AsNoTracking AnyAsync filtered by Id == req.ReportedUserId AND DeletedAt == null. If false → 404 with body code Report.UserNotFound. Soft-deleted users are treated as not found (consistent with the meet, invite, and discovery slices).",
    "Endpoint creates a new Report row: Id = Guid.NewGuid(), ReporterId = caller.Id, ReportedId = req.ReportedUserId, Reason = req.Reason (trimmed; see acceptance criteria), ReviewedAt = null, CreatedAt = TimeProvider.GetUtcNow(). Adds to context.",
    "Endpoint updates caller.LastActiveAt = TimeProvider.GetUtcNow() (consistent with every other write endpoint in the codebase).",
    "Endpoint persists Report + caller.LastActiveAt in a single SaveChangesAsync(ct).",
    "Endpoint returns 204 No Content. Intentionally no response body — the client just needs the success status; nothing to render."
  ],
  "alternate_flows": [
    "Caller already submitted reports against the same target previously → still allowed. Each submission persists a new row. The Reports rate-limit policy (5/day per reporter) is the abuse cap; moderation can dedupe later.",
    "Reason contains leading/trailing whitespace → trim before persisting. Empty after trim → 400 + Validation.ReportReasonRequired (validator emits, after-trim length 0).",
    "Caller hits the Reports rate limit (6th report in 24h) → 429 + Retry-After header from the rate-limit middleware. No endpoint code change.",
    "Reported user is soft-deleted between request build and request handling (very narrow race) → 404 + Report.UserNotFound, same as the not-found path.",
    "Caller is soft-deleted (race against an admin action) → 404 + User.NotRegistered, same as the not-registered path."
  ],
  "acceptance_criteria": [
    "POST /api/v1/reports with valid body and valid JWT returns 204 No Content with empty body.",
    "Persisted Report row has ReporterId = caller.Id (from JWT sub), ReportedId = req.ReportedUserId, Reason = trimmed req.Reason, CreatedAt = TimeProvider now, ReviewedAt = null.",
    "Caller's LastActiveAt is updated to TimeProvider now in the same SaveChangesAsync.",
    "POST returns 400 + Validation.ReportedUserIdRequired when reportedUserId is empty.",
    "POST returns 400 + Validation.ReportReasonRequired when reason is null, empty, or whitespace-only.",
    "POST returns 400 + Validation.ReportReasonTooLong when reason length > 300 characters (validator).",
    "POST at the 300-char boundary passes (300 ✓, 301 ✗); integration- or unit-tested at the boundary.",
    "POST returns 400 + Validation.CannotReportSelf when req.ReportedUserId == caller.Id. Verified by a test that submits a self-report.",
    "POST returns 401 Unauthorized when the bearer token is missing or has no sub claim.",
    "POST returns 404 + User.NotRegistered when the JWT sub does not match any User row (or the matching row is soft-deleted).",
    "POST returns 404 + Report.UserNotFound when reportedUserId is not a real user, or the matching User has DeletedAt != null.",
    "POST returns 429 with a Retry-After header when the caller exceeds the Reports rate-limit policy (5/day per authenticated user). Verified by an integration test issuing 6 reports against distinct, valid targets and asserting the 6th is 429.",
    "Duplicate reports against the same target are explicitly allowed: an integration test submits two reports against the same target back-to-back and asserts both persist (two rows in DB).",
    "The endpoint is internal sealed, inherits Endpoint<SubmitReportRequest> (no response generic since the success body is 204 NoContent), declares DontCatchExceptions(), DontThrowIfValidationFails(), Policies(nameof(AuthorizationPolicies.UsersOnly)), and .RequireRateLimiting(RateLimitPolicies.Reports) inside Description(...). Mirrors the Configure() shape of SendInviteEndpoint.",
    "The endpoint sits at src/WanderMeet.Api/Features/Reports/SubmitReport/SubmitReportEndpoint.cs with companion SubmitReportRequest.cs and SubmitReportValidator.cs in the same folder. A new ReportsFeatureConfiguration.cs at the slice root provides the Swagger tag (\"Reports\"). No DI registrations.",
    "Summary block lists every status code returned (204, 400, 401, 404, 429).",
    "All read paths use AsNoTracking. The caller is loaded tracked because it is mutated (LastActiveAt). The Report row is created and added to the tracked context.",
    "All async calls forward CancellationToken. The endpoint matches the codebase convention: parameter named ct, last position.",
    "TimeProvider is injected via the primary constructor; no DateTime.UtcNow.",
    "New error code constants are added to src/WanderMeet.Shared/ErrorCodes.cs with XML <summary> docs: Validation.ReportedUserIdRequired (\"Validation.ReportedUserIdRequired\"), Validation.ReportReasonRequired (\"Validation.ReportReasonRequired\"), Validation.ReportReasonTooLong (\"Validation.ReportReasonTooLong\"), Validation.CannotReportSelf (\"Validation.CannotReportSelf\"), and a new nested static class Report with NotFound (\"Report.UserNotFound\") — naming chosen so frontend localization keys read as \"Report.UserNotFound\" (the missing target user), distinct from User.NotRegistered (the caller's own profile is missing).",
    "Integration tests in tests/WanderMeet.Api.IntegrationTests/Features/Reports/SubmitReport/SubmitReportEndpointTests.cs: 204 happy path with DB-state assertion (Report row + caller.LastActiveAt updated), 401 no-token, 404 caller not registered, 404 target not found, 404 target soft-deleted, 400 self-report, 400 reason-too-long, 400 reason-empty, 204 duplicate-allowed (two reports against the same target both persist), 429 rate-limit-exceeded (6 reports in a row → last is 429). Every test sets a distinct X-Forwarded-For (10.60.x.y range) to avoid rate-limit leakage between tests. Each test's caller (sub) is unique to avoid Reports policy quota leakage between tests.",
    "Unit tests in tests/WanderMeet.Api.UnitTests/Features/Reports/SubmitReport/SubmitReportValidatorTests.cs cover the validator boundaries: 300-char reason passes, 301-char fails with ReportReasonTooLong, null/empty/whitespace fails with ReportReasonRequired, empty reportedUserId fails with ReportedUserIdRequired.",
    "ReportsFeatureConfiguration is auto-discovered: an integration test under tests/WanderMeet.Api.IntegrationTests/Features/Reports/ReportsFeatureConfigurationTests.cs asserts the feature is present in App.Services (mirrors MeetupsFeatureConfigurationTests).",
    "No reference from Features/Reports/* to any other feature namespace. Cross-feature shared types live under Common/ or are duplicated locally, per the architecture rules."
  ],
  "out_of_scope": [
    "Moderation UI / endpoints to triage reports (set ReviewedAt, take action). UC-303 is intake-only.",
    "Notifying admins of new reports (push, email, Slack). Out of MVP intake.",
    "Listing one's own past reports (GET /reports/mine). No client need yet.",
    "Auto-suspending the reported user after N reports. Anti-abuse heuristics belong to a later UC.",
    "Tracking which Meetup or Invite the report relates to. The current Report entity has only (Reporter, Reported, Reason). Adding a context FK is a future schema migration.",
    "Soft-deleting Report rows. Not currently planned; if moderation needs to drop a row, that is a manual DB action for now.",
    "Encrypting Reason at rest. The text is stored plain in PostgreSQL. Acceptable for the MVP intake."
  ],
  "non_functional": [
    "P95 < 250 ms for POST /reports including the rate-limit check, target-exists lookup, and one SaveChangesAsync. The slice involves at most three SQL round-trips: (1) caller load, (2) target AnyAsync, (3) insert + caller-update via SaveChanges.",
    "Rate-limit MUST be enforced at the framework middleware layer (RateLimitPolicies.Reports, 5/day per authenticated user). Endpoint code does not re-check the daily count.",
    "Reason MUST be validated server-side (length, non-empty). Never trust client-side validation.",
    "All async calls forward CancellationToken. Tests use TestContext.Current.CancellationToken.",
    "No new entities. No migration. Use the existing Report table, ReportConfiguration, and DbSet<Report>.",
    "PII handling: Reason is free-text written by users and may contain PII about the target. Storage is the existing Reports table; no extra logging of Reason content. Endpoint logs MUST NOT include the Reason payload (only Report.Id and ReporterId/ReportedId for traceability if anything is logged at all)."
  ]
}
