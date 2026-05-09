{
  "$schema": ".claude/schemas/spec.v1.json",
  "uc_id": "UC-306",
  "slug": "signalr-invites",
  "title": "Realtime invite-lifecycle events via SignalR (/hubs/invites)",
  "actors": [
    "Authenticated WanderMeet user with the mobile app foregrounded — connects to the invite hub on app open and listens for live events while the app is open. (Push delivery to backgrounded apps is UC-307.)"
  ],
  "preconditions": [
    "Caller has registered (User row exists for the JWT sub).",
    "JWT bearer auth is wired in `Program.cs` (it is — UC-201).",
    "`IInviteNotifier` already exists with `InviteSentAsync` and `InviteAcceptedAsync` methods. The `Send`, `Accept` invite endpoints already invoke it. The current `NoOpInviteNotifier` is registered in `InvitesFeatureConfiguration` and is a logging-only no-op.",
    "Phase 2 entities are applied. No new migration is needed."
  ],
  "main_flow": [
    "Mobile app, on foregrounding, opens a SignalR connection to `wss://<api-host>/hubs/invites?access_token=<jwt>` (or via the `Authorization: Bearer` header for non-browser clients). The connection negotiates protocol/transport, authenticates against the same JWT bearer scheme used for HTTP endpoints, and is bound to the user's `User.Id` via a custom `IUserIdProvider`.",
    "Server creates an `InviteHub : Hub` class under `Infrastructure/SignalR/`. The hub class is `[Authorize(Policy = nameof(AuthorizationPolicies.UsersOnly))]`. Hub has no public methods clients invoke — it is server-push-only. Lifecycle methods `OnConnectedAsync` / `OnDisconnectedAsync` log at Debug only.",
    "When `SendInviteEndpoint` calls `IInviteNotifier.InviteSentAsync(invite, ct)`, the SignalR-backed implementation looks up the receiver's `User.Id`, builds an `InviteHubInviteDto` (the same shape returned from `GET /invites/incoming`), and calls `hubContext.Clients.User(receiverId.ToString()).SendAsync(\"InviteReceived\", dto, ct)`.",
    "When `AcceptInviteEndpoint` calls `IInviteNotifier.InviteAcceptedAsync(invite, meetupId, ct)`, the SignalR-backed implementation looks up the original sender's `User.Id` and pushes `\"InviteAccepted\"` with payload `{ inviteId, meetupId, acceptedAt }` to `Clients.User(senderId.ToString())`.",
    "When `DeclineInviteEndpoint` is hit, it now also calls `IInviteNotifier.InviteDeclinedAsync(invite, ct)` (NEW method). The SignalR impl pushes `\"InviteDeclined\"` with `{ inviteId }` to the sender — silent / no toast on the client (per the roadmap: 'Silent — remove from pending list only'). NB: the receiver, NOT the sender, is the caller of decline; we push to `invite.SenderId`.",
    "A new method `IInviteNotifier.InviteExpiredAsync(invite, ct)` is added to the interface for future use by the Hangfire expiry job (UC-308). The SignalR impl pushes `\"InviteExpired\"` with `{ inviteId }` to **both** participants (`Clients.Users([senderId, receiverId])`). UC-306 wires only the interface and the SignalR-side implementation; the actual *caller* of `InviteExpiredAsync` lands in UC-308 when the Hangfire expiry job is implemented. Until then, the method is reachable from production code only via UC-308.",
    "Notifier failures must NEVER bubble past the endpoint. The existing endpoints already wrap notifier calls in `try/catch` + `LogWarning`. The SignalR impl is allowed to throw on transport errors (network blip, dead connection, etc.); the endpoint catches and continues."
  ],
  "alternate_flows": [
    "Receiver is offline (no active SignalR connection) → `Clients.User(...).SendAsync(...)` is a no-op. The HTTP endpoint still completes 201; the receiver picks up the invite via `GET /invites/incoming` on next foreground or via the FCM push (UC-307).",
    "Multiple devices for the same user (current spec is single-device per `User.FcmToken`) → SignalR's `Clients.User(userId)` fans out to ALL active connections for that user-id by default, which is the right behaviour. No code change needed.",
    "JWT expiry mid-session → SignalR connection terminates at the next negotiation. Client reconnects with a fresh token (refresh-token flow; mobile responsibility).",
    "Notifier impl crashes (bug in DTO projection) → endpoint's `try/catch` logs warning and continues. The client misses the live event but the persisted state is still correct; the next list-fetch surfaces the new invite."
  ],
  "acceptance_criteria": [
    "A new SignalR hub `InviteHub : Microsoft.AspNetCore.SignalR.Hub` lives at `src/WanderMeet.Api/Infrastructure/SignalR/InviteHub.cs`. The class is `internal sealed`, `[Authorize(Policy = nameof(AuthorizationPolicies.UsersOnly))]`, has no client-callable methods, only `OnConnectedAsync` / `OnDisconnectedAsync` overrides that log at Debug.",
    "A custom `IUserIdProvider` implementation `JwtSubUserIdProvider : Microsoft.AspNetCore.SignalR.IUserIdProvider` lives at `src/WanderMeet.Api/Infrastructure/SignalR/JwtSubUserIdProvider.cs`. It resolves `connection.User.FindFirstValue(ClaimTypes.NameIdentifier)` (the JWT sub), looks up the local `User.Id` via the registered DbContext, and returns `User.Id.ToString()`. Soft-deleted users return null (connection denied).",
    "`Program.cs` registers SignalR with `builder.Services.AddSignalR()`, registers the `IUserIdProvider` as singleton, and maps the hub at `/hubs/invites` via `app.MapHub<InviteHub>(\"/hubs/invites\")`. The mapping happens AFTER `UseAuthentication` + `UseAuthorization` and BEFORE `UseFastEndpoints`.",
    "`IInviteNotifier` gains TWO new methods: `Task InviteDeclinedAsync(Invite invite, CancellationToken ct)` and `Task InviteExpiredAsync(Invite invite, CancellationToken ct)`. Both are documented with `<summary>` per `rules/csharp-style.md#xml-documentation`.",
    "`DeclineInviteEndpoint` is updated to call `IInviteNotifier.InviteDeclinedAsync(invite, ct)` after `SaveChangesAsync`, wrapped in `try/catch` + `LogWarning` matching the existing notifier-call pattern in `SendInviteEndpoint`/`AcceptInviteEndpoint`.",
    "A new SignalR-backed implementation `SignalRInviteNotifier : IInviteNotifier` lives at `src/WanderMeet.Api/Infrastructure/SignalR/SignalRInviteNotifier.cs`. It is `internal sealed`, takes `IHubContext<InviteHub> hubContext` + `WanderMeetDbContext dbContext` (read-only) + `ILogger<SignalRInviteNotifier> logger` via primary ctor.",
    "`SignalRInviteNotifier.InviteSentAsync` projects an `InviteHubReceivedDto` (Id, sender mini, hangoutTagSlug, place mini, sentAt, expiresAt) from the persisted Invite + reads the sender's photo URL via the same projection idiom used in `SendInviteEndpoint`. Then calls `hubContext.Clients.User(invite.ReceiverId.ToString()).SendAsync(\"InviteReceived\", dto, ct)`. Soft-deleted receivers are not pushed (defensive — checks `User.DeletedAt == null`).",
    "`SignalRInviteNotifier.InviteAcceptedAsync` calls `hubContext.Clients.User(invite.SenderId.ToString()).SendAsync(\"InviteAccepted\", new InviteHubAcceptedDto(invite.Id, meetupId, invite.RespondedAt!.Value), ct)`. No DB read needed.",
    "`SignalRInviteNotifier.InviteDeclinedAsync` calls `hubContext.Clients.User(invite.SenderId.ToString()).SendAsync(\"InviteDeclined\", new InviteHubDeclinedDto(invite.Id), ct)`. No DB read needed.",
    "`SignalRInviteNotifier.InviteExpiredAsync` calls `hubContext.Clients.Users(new[] { invite.SenderId.ToString(), invite.ReceiverId.ToString() }).SendAsync(\"InviteExpired\", new InviteHubExpiredDto(invite.Id), ct)`. Pushes to both participants in a single call.",
    "`InvitesFeatureConfiguration.AddFeatureDependencies` swaps `IInviteNotifier` registration from `NoOpInviteNotifier` to `SignalRInviteNotifier`. The `NoOpInviteNotifier` class stays in the codebase as a reference / fallback for tests that want to disable realtime, but is no longer the default registration.",
    "Integration tests in tests/WanderMeet.Api.IntegrationTests/Features/Invites/Realtime/InviteHubTests.cs (using `Microsoft.AspNetCore.SignalR.Client.HubConnectionBuilder` against `WanderMeetApiFactory`'s `Server`): receiver gets `InviteReceived` after sender POSTs an invite; sender gets `InviteAccepted` after receiver PATCH-accepts; sender gets `InviteDeclined` after receiver PATCH-declines; both get `InviteExpired` when `IInviteNotifier.InviteExpiredAsync` is invoked from a test via `IServiceProvider.GetRequiredService<IInviteNotifier>()` (the Hangfire-job caller doesn't exist yet); connecting without a JWT fails with 401; the receiver does NOT receive `InviteAccepted` (only the sender does).",
    "Hub auth: `[Authorize]` on the hub rejects unauthenticated negotiation. Authentication uses the same `JwtBearer` scheme as HTTP endpoints; tokens are accepted from the `Authorization: Bearer` header (default) AND from the `access_token` query parameter for browser-based WebSocket clients (configure `JwtBearerOptions.Events.OnMessageReceived` to read `access_token` only for `Path.StartsWithSegments(\"/hubs\")`).",
    "The hub DTOs `InviteHubReceivedDto`, `InviteHubAcceptedDto`, `InviteHubDeclinedDto`, `InviteHubExpiredDto` live under `src/WanderMeet.Api/Infrastructure/SignalR/Shared/`. Records with `init` properties. JSON-serialised by the SignalR JSON protocol (default, matches the global API JSON config).",
    "Notifier failures are caught at the calling endpoint, NOT inside the notifier impl itself. The notifier methods may throw on transport errors; endpoints continue to use the existing try/catch + LogWarning shape.",
    "The Azure SignalR Service path is OUT OF SCOPE for this UC — the in-process hub is sufficient for MVP. Adding `services.AddSignalR().AddAzureSignalR(connectionString)` is a single-line config change for a future deployment task; the Hub + Notifier code is the same regardless. (Documented in the work-items doc Assumptions.)",
    "Test infrastructure: `WanderMeetApiFactory.CreateAuthenticatedSignalRConnection(string sub)` helper builds a `HubConnection` against `Server.CreateHandler()` with the right bearer token + WebSocket transport. Add this helper to the test infrastructure if absent.",
    "Endpoints are not modified except `DeclineInviteEndpoint` (one new notifier call) and the WI does NOT touch route definitions, request DTOs, response DTOs, validators, or persisted state for any existing endpoint. Existing UC-301 + UC-302 tests must continue to pass."
  ],
  "out_of_scope": [
    "Azure SignalR Service config (connection string + serverless endpoint). Deployment/CD concern.",
    "FCM push delivery (UC-307). SignalR is for foregrounded clients; FCM covers backgrounded.",
    "The Hangfire expiry job that *invokes* `InviteExpiredAsync` — UC-308.",
    "Read receipts on accepted invites (deliberately not in product per roadmap).",
    "Server-to-server hub events (admin pushes, etc.).",
    "Authoring an FCM-style payload schema. The SignalR JSON shape is the contract; mobile decodes records-with-camelCase properties.",
    "Multi-region SignalR backplane (Redis, Service Bus). Single-instance is the MVP target."
  ],
  "non_functional": [
    "P95 < 100 ms for the SignalR push call from inside `IInviteNotifier.InviteSentAsync`. The send itself is fire-and-forget across the SignalR transport; the DB lookup for the receiver-photo projection is the dominant cost.",
    "Hub DOES NOT block the endpoint — notifier exceptions propagate, endpoint catches + LogWarning, continues. The persisted invite must always succeed regardless of notifier outcome.",
    "All async notifier calls forward the cancellation token through to `SendAsync`.",
    "Hub auth uses the same JWT scheme as HTTP. Anonymous connections are rejected at negotiation time.",
    "Hub messages must NOT include the user's email, AzureAdB2CId, or any field that's not already returned by the corresponding HTTP endpoint. The SignalR DTOs are a strict subset of (or equal to) the HTTP DTOs in their shape.",
    "Notifier MUST NOT log the full DTO payload at Info+ level. Debug-level is fine; Info-level + a Guid id is the upper bound. Avoid leaking user IDs to plaintext logs."
  ]
}
