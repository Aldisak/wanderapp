{
  "$schema": ".claude/schemas/spec.v1.json",
  "uc_id": "UC-307",
  "slug": "fcm-push",
  "title": "FCM push notifications for invite lifecycle (Firebase Admin SDK)",
  "actors": [
    "Authenticated WanderMeet user receiving an invite, acceptance, or decline notification on a backgrounded mobile app — push delivered to the device's stored FCM token (UC-305)."
  ],
  "preconditions": [
    "User.FcmToken is populated for any user that should receive push (mobile client posts the token via UC-305 PATCH /users/me/fcm-token).",
    "UC-306 SignalR is shipped — SignalRInviteNotifier is the current IInviteNotifier registration.",
    "Firebase project provisioned with a service account JSON file. Local dev / tests use a NoOp client when the credential config is absent.",
    "Configuration source provides Firebase__CredentialsPath (file path) and/or Firebase__ProjectId. Missing → NoOp registration."
  ],
  "main_flow": [
    "Caller sends invite: SendInviteEndpoint persists the invite, then calls IInviteNotifier.InviteSentAsync. The composite notifier fans out to SignalRInviteNotifier (UC-306) AND a new FcmInviteNotifier (this UC). FcmInviteNotifier looks up the receiver's FcmToken; if present, formats the push (standard / 'I'm there' depending on senderIsThere), calls IFcmClient.SendAsync. If FcmToken is null/empty, push is silently skipped.",
    "Caller accepts invite: AcceptInviteEndpoint calls IInviteNotifier.InviteAcceptedAsync. Composite fans out. FcmInviteNotifier looks up the original sender's FcmToken, formats the 'See you there!' push, sends.",
    "Caller declines invite: DeclineInviteEndpoint calls IInviteNotifier.InviteDeclinedAsync. FcmInviteNotifier does nothing — decline is silent per the roadmap ('No notification to sender — intentional').",
    "Invite expires: Hangfire job (UC-308) will call IInviteNotifier.InviteExpiredAsync. FcmInviteNotifier does nothing in this UC — expiry-on-push is out of MVP. (Both SignalR and FCM push for InviteExpired are no-ops on FCM; SignalR pushes the silent UI cleanup.)",
    "Review prompt push (3h post-accept) is OUT OF SCOPE here — UC-308 Hangfire job will call IFcmClient.SendAsync directly with the review-prompt template. UC-307 only adds the IInviteNotifier-driven push triggers."
  ],
  "alternate_flows": [
    "Receiver has no FcmToken (null or empty) → silently skip the push. Persisted invite is unaffected. Logged at LogDebug.",
    "Firebase config missing (no CredentialsPath) → IFcmClient is registered as NoOpFcmClient. Production deploy without proper Firebase config silently degrades; the missing config is logged at startup as Warning so it surfaces in App Insights.",
    "FCM rejects the token (404 unregistered token) → log Warning, do NOT throw. The FcmToken should be cleared on the User row (out of scope here — handled by a future cleanup job; persisted token may stay stale).",
    "FCM transient network failure → throw from IFcmClient — composite catches at the per-impl boundary and continues. The endpoint already absorbs notifier failures via its existing try/catch + LogWarning.",
    "User has been soft-deleted between invite send and push send → still send (FcmToken is on the User row regardless of DeletedAt). Defensive: filter DeletedAt == null when looking up the FcmToken so soft-deleted users don't receive surprise pushes."
  ],
  "acceptance_criteria": [
    "A new IFcmClient interface lives at src/WanderMeet.Api/Infrastructure/Push/IFcmClient.cs with a single method: Task SendAsync(string fcmToken, string title, string body, CancellationToken ct). XML <summary> on the interface and its method.",
    "A FirebaseAdminFcmClient : IFcmClient implementation lives at src/WanderMeet.Api/Infrastructure/Push/FirebaseAdminFcmClient.cs. It uses Firebase Admin SDK (FirebaseAdmin + Google.Apis.Auth) to send via FirebaseMessaging.DefaultInstance.SendAsync(message). The Firebase app is initialised lazily on first send (or at startup via a hosted-service lifecycle method) using GoogleCredential.FromFile(credentialsPath). internal sealed.",
    "A NoOpFcmClient : IFcmClient implementation lives at src/WanderMeet.Api/Infrastructure/Push/NoOpFcmClient.cs. It logs the send at Debug and returns Task.CompletedTask. internal sealed. Used when Firebase__CredentialsPath config is missing/empty.",
    "DI registration logic in InvitesFeatureConfiguration.AddFeatureDependencies (or a new PushFeatureConfiguration if cleaner): if config[\"Firebase:CredentialsPath\"] is non-empty AND the file exists, register FirebaseAdminFcmClient as IFcmClient; else register NoOpFcmClient and log Warning '[FCM] Firebase credentials missing — push notifications disabled (using NoOp client).'",
    "A new FcmInviteNotifier : IInviteNotifier lives at src/WanderMeet.Api/Features/Invites/Realtime/FcmInviteNotifier.cs. internal sealed. Primary ctor: (IFcmClient fcmClient, WanderMeetDbContext dbContext, ILogger<FcmInviteNotifier> logger).",
    "FcmInviteNotifier.InviteSentAsync: Single AsNoTracking projection from Invites where Id == invite.Id, selecting receiver.FcmToken, sender.FirstName, place.Name, hangoutTag.Slug. If receiver FcmToken null/empty → LogDebug + skip. If senderIsThere==true → format 'Im-there' template; else format 'standard' template. Call IFcmClient.SendAsync.",
    "FcmInviteNotifier.InviteAcceptedAsync: Single AsNoTracking projection from Invites where Id == invite.Id, selecting sender.FcmToken, receiver.FirstName, place.Name. If sender FcmToken null/empty → LogDebug + skip. Format 'accepted' template. Call IFcmClient.SendAsync.",
    "FcmInviteNotifier.InviteDeclinedAsync: NoOp. Returns Task.CompletedTask immediately. LogDebug 'silent decline — no FCM push'.",
    "FcmInviteNotifier.InviteExpiredAsync: NoOp for now. Returns Task.CompletedTask immediately. LogDebug 'expiry FCM push deferred to future iteration'.",
    "A new CompositeInviteNotifier : IInviteNotifier lives at src/WanderMeet.Api/Features/Invites/Realtime/CompositeInviteNotifier.cs. internal sealed. Primary ctor: (SignalRInviteNotifier signalRNotifier, FcmInviteNotifier fcmNotifier, ILogger<CompositeInviteNotifier> logger). Each of the four IInviteNotifier methods invokes BOTH child notifiers in sequence; per-child try/catch + LogWarning ensures one failing impl does not block the other.",
    "InvitesFeatureConfiguration.AddFeatureDependencies registers SignalRInviteNotifier as itself (AddScoped<SignalRInviteNotifier>()), FcmInviteNotifier as itself (AddScoped<FcmInviteNotifier>()), and CompositeInviteNotifier as IInviteNotifier (AddScoped<IInviteNotifier, CompositeInviteNotifier>()). The previous AddScoped<IInviteNotifier, SignalRInviteNotifier> registration is removed.",
    "Push template constants live in a new static class src/WanderMeet.Api/Features/Invites/Realtime/PushTemplates.cs with format functions returning (string title, string body) tuples — exact wording from the roadmap: standard 'Coffee at {place}?' / '{senderName} wants to meet you at {placeName}.'; im-there '{senderName} is at {place} ☕' / 'They're there right now and would love some company.'; accepted 'See you there!' / '{receiverName} accepted — they're on their way to {placeName}.'. The hangoutTag slug is title-cased ('Coffee', 'Walk', etc.) using a small switch.",
    "A new NuGet reference is added to src/WanderMeet.Api/WanderMeet.Api.csproj: <PackageReference Include=\"FirebaseAdmin\" Version=\"3.4.0\" /> (or current stable matching .NET 10).",
    "Test infrastructure: a new RecordingFcmClient : IFcmClient under tests/WanderMeet.Api.IntegrationTests/Infrastructure/. Captures every Send call into IReadOnlyList<RecordedFcmSend> Sends, plus an optional Exception? ThrowOnSend hook (mirroring RecordingInviteNotifier).",
    "WanderMeetApiFactory swaps the registered IFcmClient to RecordingFcmClient (test-only override). The composite notifier still runs in tests; tests assert against RecordingFcmClient.Sends.",
    "Integration tests in tests/WanderMeet.Api.IntegrationTests/Features/Invites/Push/FcmInviteNotifierTests.cs cover: 1) SendInvite happy-path fires receiver push with 'standard' title; 2) SendInvite with senderIsThere=true fires 'im-there' title; 3) SendInvite with no receiver FcmToken silently skips; 4) AcceptInvite fires sender push with 'accepted' title; 5) AcceptInvite with no sender FcmToken silently skips; 6) DeclineInvite fires NO FCM push; 7) Composite continues if FCM throws (set RecordingFcmClient.ThrowOnSend, assert SignalR push still fires + endpoint returns 201); 8) Composite continues if SignalR throws (set RecordingInviteNotifier.ThrowOnSent, assert FCM push still fires + endpoint returns 201).",
    "Existing UC-306 InviteHubTests must continue to pass — the composite must still invoke SignalR for every event. Re-run the full Invites suite as part of verification.",
    "All async calls forward CancellationToken. No DateTime.UtcNow.",
    "PII: FCM message body MUST NOT contain User.AzureAdB2CId, User.Email, User.Bio, or any field outside what's in the documented push templates (FirstName + place name + hangoutTag display).",
    "Endpoints (SendInviteEndpoint / AcceptInviteEndpoint / DeclineInviteEndpoint) are NOT modified in this UC — only DI registration changes.",
    "Test IPs: 10.100.x.y range reserved for this UC's tests."
  ],
  "out_of_scope": [
    "Review-prompt push (3h post-meetup) — UC-308 Hangfire calls IFcmClient.SendAsync directly with the review-prompt template.",
    "FCM token cleanup on 404 unregistered. Stale tokens stay; future job triages.",
    "iOS APNS-specific payload (badge counts, sound configs). MVP uses generic Notification message.",
    "Multi-device push (one User → multiple FcmTokens). User.FcmToken is single-value per UC-305.",
    "Topic-based push or broadcasts. Only direct device pushes.",
    "Push analytics / delivery receipts.",
    "Rich-content push (images, action buttons). Plain title+body only."
  ],
  "non_functional": [
    "Push send P95 < 500 ms. Most of the time is the network round-trip to FCM; the local DB lookup is cheap (single AsNoTracking projection).",
    "Push failures (FCM transient errors, expired tokens) MUST NEVER bubble past the endpoint. The composite's per-child try/catch absorbs FcmInviteNotifier exceptions.",
    "All async calls forward CancellationToken.",
    "Firebase Admin SDK bootstrap: only one FirebaseApp instance per process. Initialisation is idempotent.",
    "Tests MUST NOT touch the real Firebase API. RecordingFcmClient is the test-mode IFcmClient.",
    "PII: push body content is the same data already exposed via API endpoints. No new PII surface.",
    "Push cadence is bounded by user actions (invite send / accept). No bulk loops; rate-limit policies on the underlying endpoints already throttle the upstream rate."
  ]
}
