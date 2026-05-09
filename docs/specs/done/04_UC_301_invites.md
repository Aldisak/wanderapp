{
  "$schema": ".claude/schemas/spec.v1.json",
  "uc_id": "UC-301",
  "slug": "invites",
  "title": "Invite lifecycle: send, accept, decline, list incoming/sent/past",
  "actors": [
    "Authenticated WanderMeet user (sender) initiating an invite",
    "Authenticated WanderMeet user (receiver) responding to or browsing invites"
  ],
  "preconditions": [
    "Caller has registered (User row exists for the JWT sub).",
    "Phase 2 entities + migrations are applied. The Invite, Meetup, HangoutTag, and Place tables exist with the foreign-key relationships defined in the data model. Invite.Status is stored as a string per the global enum convention.",
    "RateLimitPolicies.InviteSend (20/hour per user) is registered in Program.cs.",
    "An IInviteNotifier abstraction exists (or is added) so endpoints can fire 'invite received', 'invite accepted', 'invite expired' events into a no-op default for Phase 3a; Phase 3b replaces the no-op with SignalR + FCM. The notifier interface should be defined in the slice's Shared/ folder OR in WanderMeet.Infrastructure — designer chooses."
  ],
  "main_flow": [
    "Mobile app calls POST /api/v1/invites with body { receiverId, hangoutTagId, placeId, senderIsThere }.",
    "Endpoint resolves the caller's User by JWT sub. 401 if sub missing. 404 + User.NotRegistered if no User row.",
    "Validator: all four ids non-empty, senderIsThere is bool. Endpoint loads the receiver User, the HangoutTag, and the Place. Each missing → 400 with stable error code (Invite.ReceiverNotFound, Invite.HangoutTagNotFound, Invite.PlaceNotFound).",
    "Endpoint guards: receiverId != callerId (Invite.SelfInviteForbidden, 400). receiver.DeletedAt == null. hangoutTag exists. place exists and place.CityId == receiver.CityId (a 400 invariant — you can't invite someone for a place outside their current city; designer decides whether to enforce this or relax to any place).",
    "Endpoint guards: no Pending Invite already exists in either direction between caller and receiver (Invite.AlreadyPending, 409).",
    "Endpoint creates the Invite row: Status=Pending, SentAt=now, ExpiresAt=now + ValidationConstants.InviteExpiryWindow (48 h), RespondedAt=null. Saves.",
    "Endpoint fires IInviteNotifier.InviteSentAsync(invite) — Phase 3a no-op; Phase 3b pushes SignalR + FCM.",
    "Endpoint returns 201 with InviteDto. The DTO includes sender + receiver mini-profiles (Id + FirstName + first photo URL when present), hangout tag slug, place mini-info (Id + Name + Category), status, sentAt, expiresAt, respondedAt, senderIsThere.",
    "Receiver later calls PATCH /api/v1/invites/{id}/accept. Endpoint loads the Invite, scoped to the caller as receiver. 404 if not found. 409 if Status != Pending. 409 if past ExpiresAt (the background expiry service usually catches this; the endpoint must also guard).",
    "Endpoint sets invite.Status = Accepted, invite.RespondedAt = now. Creates a Meetup row { InviteId = invite.Id, UserAId = invite.SenderId, UserBId = invite.ReceiverId, PlaceId = invite.PlaceId, MetAt = now, PromptSent = false }. Saves both within one SaveChangesAsync.",
    "Endpoint fires IInviteNotifier.InviteAcceptedAsync(invite). Returns 200 with the updated InviteDto + the new Meetup.Id.",
    "Receiver alternatively calls PATCH /api/v1/invites/{id}/decline. Same scoping/preconditions. Sets Status = Declined, RespondedAt = now. NO notifier call (silent decline is intentional). Returns 200 with the updated InviteDto.",
    "Caller calls GET /api/v1/invites/incoming → list of Pending invites where caller is receiver, ordered SentAt DESC.",
    "Caller calls GET /api/v1/invites/sent → list of all invites where caller is sender, ordered SentAt DESC, all statuses.",
    "Caller calls GET /api/v1/invites/past → list of non-Pending invites where caller is sender OR receiver, ordered RespondedAt DESC NULLS LAST then SentAt DESC. Excludes Pending."
  ],
  "alternate_flows": [
    "Caller has no User row → 404 + User.NotRegistered on every endpoint (incl. lists — list endpoints return 404 if caller is not registered, NOT 200 with []).",
    "POST /invites with rate limit exceeded (>20/hour) → 429 + Retry-After.",
    "PATCH accept after invite has expired → 409 + Invite.AlreadyResolved (or Invite.Expired, designer picks).",
    "PATCH accept on someone else's invite (caller is sender, not receiver) → 404 (treat foreign invites as not found from the caller's perspective).",
    "PATCH accept twice (idempotency consideration) → 409 + Invite.AlreadyResolved."
  ],
  "acceptance_criteria": [
    "POST /api/v1/invites returns 201 with an InviteDto including a generated id, status=Pending, sentAt=TimeProvider now, expiresAt=now + 48 h, sender + receiver mini-profiles, place mini-info, hangout tag slug.",
    "Persisted Invite row has all FKs set and Status stored as the string 'Pending'.",
    "POST returns 400 + stable error codes for each of: missing/unknown receiverId, hangoutTagId, placeId; receiverId == callerId.",
    "POST returns 409 + Invite.AlreadyPending when a Pending invite already exists from caller→receiver OR receiver→caller.",
    "POST returns 401 (no token) and 404 + User.NotRegistered (token, no User row).",
    "POST applies the InviteSend rate limit policy (20/h per user) and returns 429 + Retry-After when exceeded.",
    "PATCH /api/v1/invites/{id}/accept on an own incoming Pending invite returns 200 with the updated InviteDto + meetupId. The Invite row's Status is now 'Accepted', RespondedAt is set. A Meetup row exists with InviteId == invite.Id, MetAt set from TimeProvider, PromptSent == false.",
    "PATCH accept on a non-existent invite, or an invite where caller is sender, returns 404.",
    "PATCH accept on an already-Accepted/Declined/Expired invite returns 409 + Invite.AlreadyResolved.",
    "PATCH /api/v1/invites/{id}/decline on an own incoming Pending invite returns 200 with status=Declined, RespondedAt set. No Meetup row created. The IInviteNotifier interface is NOT called for decline (silent).",
    "GET /api/v1/invites/incoming returns only Pending invites where caller is receiver, ordered SentAt DESC. Soft-deleted senders are excluded.",
    "GET /api/v1/invites/sent returns all invites where caller is sender, ordered SentAt DESC. All statuses included.",
    "GET /api/v1/invites/past returns non-Pending invites involving caller, ordered RespondedAt DESC then SentAt DESC.",
    "Endpoint files are internal sealed, inherit the FastEndpoints REPR shape, declare DontCatchExceptions(), use Send.* pattern, sit under Features/Invites/{Action}/.",
    "Validators are FastEndpoints Validator<TRequest>; never AbstractValidator.",
    "All endpoints declare Policies(nameof(AuthorizationPolicies.UsersOnly)). POST declares RequireRateLimiting(RateLimitPolicies.InviteSend); the others declare GeneralApi.",
    "All read paths use AsNoTracking + projections. Mutations load tracked.",
    "Integration tests cover: 201 happy path with DB row + Meetup absence assertion, 409 already-pending in both directions, 404 not-registered + foreign invite, 200 accept happy path with DB row + Meetup created, 200 decline happy path with no Meetup, 429 rate limit, list endpoint shapes (incoming filters Pending, sent shows all statuses, past excludes Pending). Use distinct X-Forwarded-For per test.",
    "Unit tests: validator failure cases for each invalid input shape; trust-score-implication test (sending/accepting/declining does NOT change User.TrustScore — that only changes via reviews, future UC-302).",
    "IInviteNotifier interface is sealed and minimal: at least InviteSentAsync(Invite invite, CancellationToken ct) and InviteAcceptedAsync(Invite invite, Guid meetupId, CancellationToken ct). Default registered implementation in Phase 3a is a no-op + ILogger trace at Debug level. Phase 3b will replace with SignalR + FCM."
  ],
  "out_of_scope": [
    "SignalR hub implementation — Phase 3b.",
    "FCM push notifications — Phase 3b.",
    "Invite-expiry background service (UC-304) — sets Status from Pending to Expired after 48 h. Endpoints must still guard against expired pending invites synchronously, but the bulk Pending→Expired sweep is a separate UC.",
    "Review prompts after meetup — Phase 3b (needs FCM) and UC-302 / UC-304b.",
    "Trust score recalculation — UC-302 (review submission triggers it).",
    "Cancellation by sender — out of MVP. Sender cannot recall a sent invite.",
    "Bulk invite operations — single-recipient only.",
    "Block / mute features — separate slice if needed.",
    "Place-belongs-to-receiver-city enforcement may be deferred to UC-302 if designer decides it adds friction without security value. Designer makes the call."
  ],
  "non_functional": [
    "P95 < 400 ms for POST /invites and the two PATCH endpoints.",
    "P95 < 300 ms for the three list endpoints.",
    "Notifier failures (Phase 3b) MUST NOT bubble past the endpoint. Wrap notifier calls in try/catch and log a warning. The persisted state is the source of truth; missed notifications are a degraded UX, not a 500.",
    "All async calls forward CancellationToken. Tests use TestContext.Current.CancellationToken.",
    "No new entities. No migration. Use the existing Invite, Meetup, User, HangoutTag, Place, UserPhoto tables.",
    "InviteDto must NOT leak the receiver's AzureAdB2CId or any other sensitive identity field.",
    "The IInviteNotifier no-op MUST be the only implementation registered in Phase 3a; verify by an integration test that asserts no real network calls leave the process during a happy-path send/accept."
  ]
}
