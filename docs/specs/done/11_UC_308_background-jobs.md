{
  "$schema": ".claude/schemas/spec.v1.json",
  "uc_id": "UC-308",
  "slug": "background-jobs",
  "title": "Hangfire background jobs: invite expiry, review prompt, sink inactive profiles",
  "actors": [
    "System (Hangfire scheduler) — recurring jobs run on a schedule with no user actor.",
    "Authenticated WanderMeet users — receive the side-effects (push notifications when their meetup hits the 3h review-prompt mark; have IsOpenToday silently flipped off when inactive)."
  ],
  "preconditions": [
    "UC-301 Invites + UC-302 Meetups + UC-306 SignalR + UC-307 FCM are shipped. IInviteNotifier (Composite) and IFcmClient are registered in DI.",
    "Meetup.PromptSent column exists (it does — UC-302 added it).",
    "Postgres connection string is configured (it is). Hangfire will use the same database with its own schema.",
    "Hangfire NuGet packages: Hangfire.AspNetCore + Hangfire.PostgreSql added to WanderMeet.Api.csproj."
  ],
  "main_flow": [
    "InviteExpiryJob runs every 5 minutes via Hangfire RecurringJob.",
    "Query: dbContext.Invites.Where(i => i.Status == InviteStatus.Pending AND i.ExpiresAt <= now). For each, set Status=Expired, RespondedAt=now. Single SaveChangesAsync.",
    "For each newly-expired invite, invoke IInviteNotifier.InviteExpiredAsync(invite, ct). The composite fans out to SignalR (push 'InviteExpired' to both participants) and FCM (no-op).",
    "ReviewPromptJob runs every 5 minutes.",
    "Query: dbContext.Meetups.Where(m => !m.PromptSent AND m.MetAt + 3h <= now). Cap at Take(100) per run to avoid burst load.",
    "For each meetup that needs a prompt: lookup BOTH participants' FcmTokens (single query joining Users twice). For each non-empty token, call IFcmClient.SendAsync with the review-prompt template ('How did it go?' / 'You met {otherName} — let them know how it was.'). Then set Meetup.PromptSent = true. Single SaveChangesAsync per batch.",
    "SinkInactiveProfilesJob runs hourly.",
    "Query: dbContext.Users.Where(u => u.IsOpenToday AND u.LastActiveAt < now - 24h). UPDATE: set IsOpenToday=false. Use ExecuteUpdateAsync for a single round-trip. No SignalR/FCM event — silent UI cleanup on next user fetch.",
    "Hangfire dashboard mounted at /hangfire — protected by AuthorizationPolicies.UsersOnly. The dashboard is read-only for users; admin-grade trigger/retry actions remain disabled in this UC (Phase 4 territory)."
  ],
  "alternate_flows": [
    "Invite already expired by some other path (manual admin update) → the job skips it because Status != Pending.",
    "Meetup PromptSent=true (already prompted) → skipped by query filter.",
    "Both participants have null FcmToken → ReviewPromptJob still flips PromptSent=true (the DB transition is the idempotency anchor; no point retrying just because the user has no device token).",
    "User reactivates between job tick and SaveChanges → ExecuteUpdateAsync filters atomically; race is benign (worst case: a re-active user gets flipped off and the next API request flips them back via LastActiveAt update).",
    "Hangfire job throws → Hangfire's default retry policy kicks in (3 retries with exponential backoff). After exhaustion, Hangfire moves the job to the failed bucket; engineers triage via the dashboard."
  ],
  "acceptance_criteria": [
    "Hangfire is wired in Program.cs: builder.Services.AddHangfire(c => c.UsePostgreSqlStorage(connectionString)) + builder.Services.AddHangfireServer(opts => { opts.WorkerCount = 1; opts.Queues = [\"default\"]; }) + app.MapHangfireDashboard(\"/hangfire\", new DashboardOptions { Authorization = [new HangfireDashboardAuthorizationFilter()] }) — the filter checks IHttpContextAccessor for an authenticated user via AuthorizationPolicies.UsersOnly.",
    "NuGet packages added to src/WanderMeet.Api/WanderMeet.Api.csproj: Hangfire.AspNetCore (latest stable matching .NET 10), Hangfire.PostgreSql (latest stable), Hangfire.Core (transitive — no explicit pin needed).",
    "Three Hangfire job classes live under src/WanderMeet.Api/Infrastructure/Jobs/: InviteExpiryJob, ReviewPromptJob, SinkInactiveProfilesJob. Each is internal sealed, primary ctor with the deps it needs (WanderMeetDbContext + TimeProvider + ILogger; ReviewPromptJob also takes IFcmClient).",
    "Each job exposes a single public method (e.g. ExecuteAsync) that Hangfire invokes. Jobs are NOT MediatR/IRequest handlers — they are POCO classes with a single async method (Hangfire convention).",
    "Recurring registration runs at startup via a JobsStartupHostedService (IHostedService) that, in StartAsync, calls IRecurringJobManager.AddOrUpdate<TJob>(\"job-id\", j => j.ExecuteAsync(CancellationToken.None), cron). Cron schedules: InviteExpiryJob → */5 * * * *, ReviewPromptJob → */5 * * * *, SinkInactiveProfilesJob → 0 * * * *.",
    "InviteExpiryJob: scans Pending invites with ExpiresAt <= TimeProvider.GetUtcNow(); flips Status=Expired + RespondedAt=now; persists in a single SaveChangesAsync; then iterates the newly-expired list and fires IInviteNotifier.InviteExpiredAsync per row, wrapped in try/catch + LogWarning so a notifier failure does not roll back the persisted state. Cap per run at Take(500).",
    "ReviewPromptJob: scans Meetups with !PromptSent AND MetAt + 3h <= now (where 3h = ValidationConstants.ReviewPromptDelay = TimeSpan.FromHours(3) — add this constant to ValidationConstants.cs). For each meetup, fetch both participants' FcmTokens + the OTHER participant's FirstName via a single AsNoTracking projection. Format the review-prompt push: title='How did it go?', body='You met {otherFirstName} — let them know how it was.' Call IFcmClient.SendAsync per non-null token, wrapped in try/catch + LogWarning. Set PromptSent=true regardless of FCM outcome (DB transition is the idempotency anchor). Cap at Take(100).",
    "SinkInactiveProfilesJob: runs `dbContext.Users.Where(u => u.IsOpenToday && u.LastActiveAt < timeProvider.GetUtcNow() - TimeSpan.FromHours(24)).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsOpenToday, false), ct)`. Single SQL UPDATE round-trip. Logs the affected-row count at LogInformation.",
    "Hangfire dashboard authorization filter `HangfireDashboardAuthorizationFilter` lives at src/WanderMeet.Api/Infrastructure/Jobs/HangfireDashboardAuthorizationFilter.cs. internal sealed. Implements Hangfire.Dashboard.IDashboardAuthorizationFilter. Authorize(DashboardContext) returns true iff context.GetHttpContext().User.Identity?.IsAuthenticated == true. Read-only by default — admin-grade actions are out of scope.",
    "Tests: integration tests in tests/WanderMeet.Api.IntegrationTests/Infrastructure/Jobs/ verify each job's behaviour by directly resolving the job class from App.Services and calling ExecuteAsync (NOT via Hangfire scheduler — that introduces test flakiness). Use App.FakeTimeProvider to control 'now'. RecordingInviteNotifier asserts InviteExpiredAsync was fired for each expired invite. RecordingFcmClient asserts review-prompt sends with the exact title/body. Hangfire test setup (UseInMemoryStorage instead of Postgres) can be wired in WanderMeetApiFactory if the test fixture currently fails to start with HangfireServer; alternative: AddHangfire is left out of the test fixture and the job classes are tested as POCOs via direct DI resolution.",
    "Test: InviteExpiryJob_ExecuteAsync_PendingInvitesPastExpiry_AreFlippedToExpiredAndNotifierFires.",
    "Test: InviteExpiryJob_ExecuteAsync_PendingInvitesNotYetExpired_LeftAlone.",
    "Test: InviteExpiryJob_ExecuteAsync_NotifierThrows_PersistedStateUnchanged.",
    "Test: ReviewPromptJob_ExecuteAsync_MeetupOver3hAndPromptNotSent_FiresFcmPushAndSetsPromptSent.",
    "Test: ReviewPromptJob_ExecuteAsync_BothParticipantsHaveNoFcmToken_StillSetsPromptSentTrue.",
    "Test: ReviewPromptJob_ExecuteAsync_FcmThrows_PromptSentStillFlippedTrue.",
    "Test: ReviewPromptJob_ExecuteAsync_MeetupUnder3h_LeftAlone.",
    "Test: SinkInactiveProfilesJob_ExecuteAsync_UsersOpenTodayWithLastActiveBeyond24h_AreFlippedToFalse.",
    "Test: SinkInactiveProfilesJob_ExecuteAsync_UsersWithRecentActivity_LeftAlone.",
    "Test: HangfireDashboardAuthorizationFilter_AuthenticatedUser_ReturnsTrue (unit).",
    "Test: HangfireDashboardAuthorizationFilter_AnonymousUser_ReturnsFalse (unit).",
    "All async calls forward CancellationToken. TimeProvider injected via primary ctor. No DateTime.UtcNow.",
    "Hangfire job classes are NOT registered as keyed services — Hangfire's Activator resolves them by type. Register each as Scoped via services.AddScoped<InviteExpiryJob>() etc. in a new JobsFeatureConfiguration (rules/architecture.md#feature-configuration).",
    "JobsStartupHostedService at src/WanderMeet.Api/Infrastructure/Jobs/JobsStartupHostedService.cs (internal sealed, IHostedService). StartAsync uses IRecurringJobManager to register all three recurring jobs. Registered via services.AddHostedService<JobsStartupHostedService>().",
    "Test IPs: 10.110.x.y range reserved for any HTTP-touching tests in this UC (e.g. dashboard auth filter integration test).",
    "PII: review-prompt push body interpolates the OTHER participant's FirstName only — same data already exposed via the existing user public profile. No leakage of email / AzureAdB2CId / FcmToken / Bio.",
    "Job retry policy: rely on Hangfire's default automatic retry (3 attempts with exponential backoff). Do NOT add custom retry attributes; the default is appropriate for transient DB hiccups."
  ],
  "out_of_scope": [
    "Google Places sync job (roadmap task 3.4 (d)). Defer — not blocking the MVP user flow.",
    "Hangfire dashboard admin-grade actions (manual triggers, retry-from-failed). Read-only is enough for monitoring.",
    "Distributed-lock guarantees against multiple Hangfire servers running the same recurring job. MVP runs single-instance Hangfire; the WorkerCount=1 guard already serializes per node.",
    "Per-user opt-out for review-prompt push (e.g. 'don't send me prompts'). Future preference toggle.",
    "Job dashboards / metrics in App Insights. Hangfire's own dashboard is sufficient.",
    "Manual invocation API endpoints (POST /jobs/run-now). Engineering-only; out of MVP."
  ],
  "non_functional": [
    "InviteExpiryJob P95 < 5 s for batches up to 500 invites. Single bulk UPDATE-then-iterate-notifier. The notifier fan-out is sequential; if it becomes the bottleneck, parallelise in a future iteration.",
    "ReviewPromptJob P95 < 10 s for batches up to 100 meetups (FCM round-trips dominate).",
    "SinkInactiveProfilesJob P95 < 1 s — single UPDATE statement.",
    "All recurring jobs MUST be idempotent at the DB level. Re-running the same tick must not double-fire notifications nor double-flip flags. The PromptSent column + Status transition guarantee this.",
    "Hangfire dashboard endpoint is rate-limited by RateLimitPolicies.GeneralApi (or a dedicated lower limit if accidental hot-loops are a concern). Authentication is mandatory.",
    "All async calls forward CancellationToken — Hangfire's IJobCancellationToken bridge can wrap as needed.",
    "PII: FCM logs do not include FcmToken; logger calls reference only Meetup.Id / Invite.Id / User.Id."
  ]
}
