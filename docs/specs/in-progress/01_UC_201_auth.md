{
  "$schema": ".claude/schemas/spec.v1.json",
  "uc_id": "UC-201",
  "slug": "auth",
  "title": "User registration and token refresh via Azure AD B2C",
  "actors": [
    "Mobile app user authenticated against Azure AD B2C"
  ],
  "preconditions": [
    "User has completed Azure AD B2C sign-up/sign-in and the mobile app holds a valid B2C access token + refresh token.",
    "Phase 2.1/2.2 entities, DbContext, and migration are applied (Users table exists with unique azure_ad_b2c_id index).",
    "AzureAdB2C configuration section (Instance, TenantId, PolicyId, ClientId) is bound at startup; JWT bearer is wired in Program.cs."
  ],
  "main_flow": [
    "Mobile app calls POST /api/v1/auth/register with Authorization: Bearer <B2C access token> and body { firstName }.",
    "JwtBearer middleware validates the access token (issuer, audience, lifetime, RS256 signature).",
    "Endpoint reads the JWT subject claim ('sub' / NameIdentifier) — that is the Azure AD B2C user id.",
    "Endpoint checks for an existing User row with AzureAdB2CId == sub; if found, returns 409 conflict.",
    "Endpoint creates a new User { Id = Guid.NewGuid(), AzureAdB2CId = sub, FirstName, CreatedAt = now, LastActiveAt = now } and saves.",
    "Endpoint returns 201 with a UserDto (Id, FirstName, IsIdVerified, IsOpenToday, IsOpenToRomance, TrustScore, MeetupCount, CitiesCount, CreatedAt).",
    "Mobile app later calls POST /api/v1/auth/refresh with body { refreshToken }.",
    "Endpoint posts to the configured Azure AD B2C token endpoint with grant_type=refresh_token and the supplied refresh token, returns the new access and refresh tokens to the caller."
  ],
  "alternate_flows": [
    "Bearer token missing or invalid → JwtBearer middleware short-circuits with 401; rate limiter still records the attempt.",
    "AzureAdB2C section not configured at startup → /auth/register and /auth/refresh return 503 with a stable error code.",
    "B2C token endpoint returns non-success on /auth/refresh → endpoint maps to 401 unauthorized; do not leak provider error body."
  ],
  "acceptance_criteria": [
    "POST /api/v1/auth/register returns 201 + UserDto on first registration; the User row has AzureAdB2CId equal to the JWT 'sub' claim, FirstName from the body, LastActiveAt set from TimeProvider.",
    "POST /api/v1/auth/register returns 409 with stable error code Auth.AlreadyRegistered when AzureAdB2CId already exists.",
    "POST /api/v1/auth/register returns 401 when no Bearer token is presented or the token fails B2C validation.",
    "POST /api/v1/auth/register returns 400 with code Validation.FirstNameRequired when firstName is missing/whitespace; 400 with code Validation.FirstNameTooLong when length exceeds ValidationConstants.FirstNameMaxLength.",
    "Both endpoints apply the existing AuthEndpoints rate-limit policy (10 req/min/IP) and emit Retry-After on 429.",
    "POST /api/v1/auth/refresh returns 200 with { accessToken, refreshToken } on a valid B2C refresh token.",
    "POST /api/v1/auth/refresh returns 401 when the refresh token is rejected by B2C; the client never receives the upstream error body.",
    "Both endpoints have FastEndpoints Validator<TRequest> classes; Register validator covers FirstName length; Refresh validator covers refreshToken non-empty.",
    "Endpoint classes are internal sealed, inherit Endpoint<TReq,TRes>, declare DontCatchExceptions(), use Send.* pattern, sit under Features/Auth/Register and Features/Auth/RefreshToken with an AuthFeatureConfiguration in Features/Auth.",
    "Integration test: register-happy-path creates a row and returns 201; uses Testcontainers PostgreSQL, Respawn, and the same JWT validation by overriding the JwtBearer authority with a test signing key.",
    "Unit tests: validator failure cases for each invalid input; endpoint tests for 401 (no token), 409 (duplicate azureId)."
  ],
  "out_of_scope": [
    "Azure AD B2C tenant provisioning, user-flow setup, or app registration (operator task).",
    "Stripe Identity ID-verification toggle (handled by the Users update slice).",
    "Photo upload, profile detail, discovery, or any other feature slice.",
    "Refresh-token storage on the backend — refresh is a stateless proxy to B2C."
  ],
  "non_functional": [
    "P95 < 300 ms for /auth/register on a warm container.",
    "Both endpoints log structured events via Serilog including correlation id, but never log access/refresh tokens.",
    "Existing global exception handler covers infrastructure faults; endpoints do not catch DbUpdateException for unique-violation, they pre-check existence first.",
    "All async calls forward the CancellationToken; tests pass TestContext.Current.CancellationToken."
  ]
}
