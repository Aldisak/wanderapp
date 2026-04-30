# Roadmap

# ☕ Wander — Claude Code Build Roadmap

**Flutter · .NET 8 · Azure · Solo engineer**

---

## Overview

This is the complete build brief for Claude Code. It contains the full technical architecture, data models, API contracts, and phase-by-phase task lists needed to build the Wander MVP from scratch. **Follow phases sequentially. Each phase has clear outputs that gate the next phase.**

> **Stack at a glance**
> Mobile: Flutter (iOS + Android, single codebase) · Backend: .NET 8 + FastEndpoints (Vertical Slice Architecture) · Database: PostgreSQL + PostGIS (Azure Database for PostgreSQL Flexible Server) · Realtime: Azure SignalR Service · Storage: Azure Blob Storage · Push: Firebase Cloud Messaging (FCM) · Maps & places: Google Places API · ID verification: Stripe Identity · Auth: Azure AD B2C · Hosting: Azure Container Apps · Security: .NET 8 rate limiting, HSTS, security headers, SAS-scoped blob access

---

## Technical Architecture

### Backend — .NET 10 / FastEndpoints / Vertical Slice Architecture

| Concern | Decision |
|---|---|
| Pattern | Vertical Slice Architecture (VSA). Each feature is a self-contained slice: one folder per feature containing Endpoint, Request, Response, Validator, and any domain logic. No shared Application/Domain/Infrastructure layers. Features live at: `/Features/{FeatureName}/{Action}/` e.g. `/Features/Invites/Send/SendInviteEndpoint.cs` |
| API framework | FastEndpoints (replaces controllers). Each endpoint is a class inheriting `Endpoint<TRequest, TResponse>`. Auto-discovers all endpoints on startup. Declarative route + HTTP verb per class. |
| Auth | Azure AD B2C for identity. JWT bearer tokens. Refresh token rotation. FastEndpoints JwtBearer integration — `.Roles()` or `.Policies()` on each endpoint class. |
| Validation | FastEndpoints built-in validator (`AbstractValidator<TRequest>` per endpoint). Validation runs before handler — invalid requests never reach business logic. |
| ORM | Entity Framework Core 8 with Npgsql provider. Code-first migrations. DbContext injected directly into endpoint handlers — no repository pattern overhead. |
| Background jobs | Hangfire (Azure-hosted). Dashboard behind auth. Jobs: invite expiry, review prompts, inactive profile sink, Google Places sync. |
| Logging | Serilog structured logging → Azure Application Insights. Request/response logging middleware. Correlation IDs on all requests. |
| Secrets | Azure Key Vault. Container Apps Key Vault references. Never in appsettings.json or env vars directly. |

### Security

| Concern | Implementation |
|---|---|
| Rate limiting | .NET 8 built-in `RateLimiterMiddleware`. Policies: **GeneralApi**: 100 req/min per IP · **AuthEndpoints**: 10 req/min per IP (brute-force prevention) · **InviteSend**: 20 invites/hour per user · **Discovery**: 60 req/min per user · **Reports**: 5/day per user. Exceeded requests → HTTP 429 + `Retry-After` header. |
| HTTPS | Enforced at Container Apps ingress. HSTS header. HTTP → HTTPS redirect. TLS 1.2 minimum. |
| CORS | Strict allow-list. Only Wander mobile app origin permitted. No wildcard. |
| JWT validation | Azure AD B2C public key validation. Validate issuer, audience, expiry, signing algorithm (RS256). Reject tokens with `none` algorithm. |
| Input sanitisation | FastEndpoints validates all inputs via `AbstractValidator` before handler executes. Max length enforced on all string fields. No raw SQL — EF Core parameterised queries only. |
| SQL injection | EF Core with parameterised queries throughout. PostGIS spatial queries via `EF.Functions`. Raw SQL explicitly forbidden. |
| Blob SAS tokens | Short-lived (10 min) write-only SAS tokens for photo uploads. Read via CDN. Tokens scoped to `users/{userId}/photos/` prefix — users can never generate a SAS for another user's prefix. |
| Security headers | Middleware on all responses: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Permissions-Policy: geolocation=(), camera=(), microphone=()` |
| Soft deletes | Deleted users cannot authenticate — `deleted_at` checked on every JWT validation via global auth filter. |
| Report throttle | Max 5 reports/day per user (rate limit policy). Prevents report-spam abuse. |
| Hangfire dashboard | Protected behind Admin role claim. Not publicly accessible. Azure AD authentication required. |

### VSA Folder Structure

Every feature is fully self-contained. Nothing crosses slice boundaries except shared infrastructure.

```
Wander.API/
  Features/
    Invites/
      Send/
        SendInviteEndpoint.cs    ← Endpoint<Request, Response>
        SendInviteRequest.cs     ← record with all input fields
        SendInviteResponse.cs    ← record with output fields
        SendInviteValidator.cs   ← AbstractValidator<SendInviteRequest>
      Accept/
        AcceptInviteEndpoint.cs
        ...
      Decline/
      GetIncoming/
      GetSent/
      GetPast/
    Users/
      GetMe/
      UpdateMe/
      GetById/
      ToggleOpenToday/
      UploadPhoto/
      DeletePhoto/
      AddCity/
      UpdateCity/
    Discovery/
      Discover/
      GetArriving/
    Places/
      SuggestPlaces/
      ListPlaces/
      GetPlace/
    Meetups/
      SubmitReview/
      GetPendingReview/
    Auth/
      Register/
      RefreshToken/
    Reports/
      SubmitReport/
    Cities/
      SearchCities/
      GetCity/
  Infrastructure/
    Persistence/
      WanderDbContext.cs
      Migrations/
    SignalR/
      InviteHub.cs
    Jobs/
      InviteExpiryJob.cs
      ReviewPromptJob.cs
      SinkInactiveProfilesJob.cs
      SyncPlacesJob.cs
    Notifications/
      FcmService.cs
    Blob/
      BlobService.cs
  Common/
    Middleware/
      SecurityHeadersMiddleware.cs
      CorrelationIdMiddleware.cs
    Extensions/
      ServiceCollectionExtensions.cs
    Models/
      PagedResult.cs
      ApiResponse.cs
```

> **FastEndpoints slice anatomy** — Every endpoint class: (1) inherits `Endpoint<TRequest, TResponse>`, (2) declares route + HTTP verb in `Configure()`, (3) declares rate limit policy in `Configure()`, (4) declares auth requirement in `Configure()`, (5) all logic in `HandleAsync()`. Validator class lives alongside and runs automatically before `HandleAsync` is called. Never share handlers between slices.

### Database — PostgreSQL + PostGIS

| Concern | Decision |
|---|---|
| Host | Azure Database for PostgreSQL Flexible Server (General Purpose tier) |
| Spatial | PostGIS extension. `ST_DWithin` for proximity filtering. `ST_Distance` for sorting. |
| Key indexes | GiST index on `user.location`. Composite index on `(city_id, is_open_today, last_active_at)`. B-tree on `trust_score`. |
| Soft deletes | All user-generated content uses `deleted_at` nullable timestamp — never hard delete. |

### Mobile — Flutter

| Concern | Decision |
|---|---|
| State management | Riverpod (`AsyncNotifier` pattern throughout) |
| Navigation | GoRouter with deep link support for invite notifications |
| HTTP client | Dio with interceptors for auth token injection and refresh |
| Realtime | SignalR client (`signalr_flutter` package) for live invite events |
| Local storage | `flutter_secure_storage` for tokens. Hive for lightweight local cache. |
| Maps / places | `google_maps_flutter` + Google Places API |
| Push notifications | `firebase_messaging`. FCM for both iOS and Android. |
| Image handling | `image_picker` + `image_cropper`. Upload direct to Azure Blob via SAS token. |
| Theming | Custom `ThemeData`. Brand color: `#534AB7`. Design tokens match wireframes exactly. |

### Azure Services

| Service | Purpose |
|---|---|
| Container Apps | Backend API deployed as container. Auto-scaling, zero downtime deploys. |
| Azure DB PostgreSQL | Flexible Server. Enable PostGIS extension on first deploy. |
| Azure Blob Storage | Profile photos, ID verification uploads (short-lived SAS URLs). |
| Azure SignalR Service | Serverless mode. Hub for invite sent/accepted/declined events. |
| Azure AD B2C | User identity, OAuth flows, token issuance. |
| Azure Key Vault | All secrets: DB connection string, FCM key, Google Places key, Stripe key. |
| Azure CDN | Front Blob Storage for profile photo delivery. |
| Application Insights | Full telemetry, request tracing, error alerting. |

---

## Core Data Models

> These are canonical. Implement field names, types, and constraints exactly as written — they define API contracts and database schema.

### Users
```
Users
  id                  uuid          PK
  azure_ad_b2c_id     string        UNIQUE NOT NULL
  first_name          string        NOT NULL
  bio                 string        max 160 chars
  is_id_verified      bool          default false
  is_open_today       bool          default false
  is_open_to_romance  bool          default false
  last_active_at      timestamptz   NOT NULL
  location            geography     PostGIS POINT (city-level only)
  city_id             uuid          FK → Cities
  trust_score         int           0–100, computed from meetups
  meetup_count        int           default 0
  cities_count        int           default 0
  years_nomading      decimal(3,1)
  created_at          timestamptz   NOT NULL
  deleted_at          timestamptz   nullable
```

### UserPhotos
```
UserPhotos
  id          uuid
  user_id     uuid    FK → Users
  blob_url    string  Azure CDN URL
  order       int     0–3 (max 4 photos)
  created_at  timestamptz
```

### HangoutTags
```
HangoutTags  (enum-style seed data)
  id    uuid
  slug  string   coffee | walk | food | explore | cowork
  label string   Coffee | Walk | Food | Explore | Cowork
  emoji string

UserHangoutTags  (join table)
  user_id        uuid  FK → Users
  hangout_tag_id uuid  FK → HangoutTags
```

### Cities
```
Cities
  id          uuid
  name        string
  country     string  ISO 3166-1 alpha-2
  location    geography  PostGIS POINT
  created_at  timestamptz
```

### UserCities (travel history)
```
UserCities
  id          uuid
  user_id     uuid    FK → Users
  city_id     uuid    FK → Cities
  arrived_at  timestamptz
  departed_at timestamptz  nullable = currently here
  is_current  bool         computed
```

### Places
```
Places
  id                  uuid
  google_place_id     string      UNIQUE
  name                string
  city_id             uuid        FK → Cities
  location            geography   PostGIS POINT
  category            string      cafe | cowork | park | restaurant | landmark
  has_wifi            bool
  is_quiet            bool
  is_solo_friendly    bool
  google_rating       decimal(2,1)
  wander_meetup_count int         default 0  — incremented on confirmed meetup
  is_sponsored        bool        default false
  sponsor_perk        string      nullable  e.g. 'Free day pass for Wander users'
  created_at          timestamptz
```

### Invites
```
Invites
  id               uuid
  sender_id        uuid         FK → Users
  receiver_id      uuid         FK → Users
  hangout_tag_id   uuid         FK → HangoutTags
  place_id         uuid         FK → Places
  sender_is_there  bool         default false  — 'I'm already here' toggle
  status           string       pending | accepted | declined | expired
  sent_at          timestamptz
  responded_at     timestamptz  nullable
  expires_at       timestamptz  sent_at + 48h (background job)
```

### Meetups
```
Meetups
  id           uuid
  invite_id    uuid         FK → Invites  UNIQUE
  user_a_id    uuid         FK → Users
  user_b_id    uuid         FK → Users
  place_id     uuid         FK → Places
  met_at       timestamptz  default now()
  prompt_sent  bool         default false
```

### MeetupReviews
```
MeetupReviews
  id               uuid
  meetup_id        uuid   FK → Meetups
  reviewer_id      uuid   FK → Users
  reviewee_id      uuid   FK → Users
  did_meet         bool
  felt_safe        bool   default false
  good_convo       bool   default false
  would_meet_again bool   default false
  text             string nullable  max 120 chars
  created_at       timestamptz
```

### Reports
```
Reports
  id           uuid
  reporter_id  uuid    FK → Users
  reported_id  uuid    FK → Users
  reason       string  max 300 chars
  created_at   timestamptz
  reviewed_at  timestamptz  nullable
```

### Trust Score Algorithm
```
trust_score = CLAMP(0, 100, base_score)

base_score =
  (meetup_count       × 6) +
  (felt_safe_count    × 4) +
  (would_meet_again   × 3) +
  (good_convo_count   × 2)

Recalculated server-side on every new review submission.
Stored as integer on Users table.
Never trust client-sent scores.
```

---

## API Contracts

All endpoints prefixed `/api/v1`. All requests require `Authorization: Bearer {token}` except `/auth/*`. All responses: `{ data, error, pagination? }`.

### Auth
| Endpoint | Description |
|---|---|
| `POST /auth/register` | Register after Azure AD B2C signup. Body: `{ azureId, firstName }`. Returns: `UserDto`. Rate limit: AuthEndpoints. |
| `POST /auth/refresh` | Refresh JWT. Body: `{ refreshToken }`. Returns: `{ accessToken, refreshToken }`. Rate limit: AuthEndpoints. |

### Users
| Endpoint | Description |
|---|---|
| `GET /users/me` | Current user's full profile. |
| `PATCH /users/me` | Update bio, hangout tags, open_to_romance. Partial update. |
| `GET /users/{id}` | Another user's public profile. Returns `PublicUserDto` (no sensitive fields). |
| `POST /users/me/photos` | Returns SAS URL for direct Azure Blob upload + photo record. SAS scoped to `users/{userId}/photos/`. |
| `DELETE /users/me/photos/{id}` | Remove a photo. |
| `POST /users/me/cities` | Add city to travel history. Body: `{ cityId, arrivedAt }`. |
| `PATCH /users/me/cities/{id}` | Update city — set `departedAt` when leaving. |
| `PATCH /users/me/open-today` | Toggle open_today. Body: `{ isOpen: bool }`. |
| `PATCH /users/me/fcm-token` | Update FCM device token. Body: `{ token }`. Called on every app open. |

### Discovery
| Endpoint | Description |
|---|---|
| `GET /discover` | Paginated nearby users. Query: `cityId`, `hangoutTagSlug?`, `limit=20`, `cursor?`. Filters: `is_open_today=true`, `last_active_at > now-72h`, not already invited. Sort: `open_today DESC`, `trust_score DESC`, `last_active_at DESC`. Uses PostGIS `ST_DWithin`. |
| `GET /discover/arriving` | Users arriving soon in this city. Used for empty state 'arriving soon' cards. |

### Places
| Endpoint | Description |
|---|---|
| `GET /places/suggest` | 3 nomad-rated places for invite. Query: `cityId`, `hangoutTagSlug`, `lat`, `lng`. Sorted by `wander_meetup_count DESC`, distance ASC. Slot 3 may be sponsored. |
| `GET /places` | All places in city. Query: `cityId`, `category?`. Used for Places tab. |
| `GET /places/{id}` | Single place detail. |
| `POST /places/sync` | Internal — sync from Google Places API. Called by Hangfire job. |

### Invites
| Endpoint | Description |
|---|---|
| `POST /invites` | Send invite. Body: `{ receiverId, hangoutTagId, placeId, senderIsThere }`. Triggers FCM push + SignalR event. Rate limit: InviteSend. |
| `GET /invites/incoming` | Pending incoming invites. |
| `GET /invites/sent` | Sent invites with status. |
| `GET /invites/past` | Past invites (accepted / declined / expired). |
| `PATCH /invites/{id}/accept` | Accept invite. Creates Meetup record. Notifies sender. |
| `PATCH /invites/{id}/decline` | Decline. Status → declined. **No notification to sender — intentional.** |

### Meetups & Reviews
| Endpoint | Description |
|---|---|
| `GET /meetups/pending-review` | Meetups where current user hasn't reviewed yet. Called on app open. |
| `POST /meetups/{id}/review` | Submit review. Body: `{ didMeet, feltSafe, goodConvo, wouldMeetAgain, text? }`. Triggers trust score recalculation. Increments `wander_meetup_count` on place if `didMeet=true`. |
| `GET /users/{id}/reviews` | Public reviews shown on profile. |

### Reports
| Endpoint | Description |
|---|---|
| `POST /reports` | Report a user. Body: `{ reportedId, reason }`. Rate limit: Reports (5/day). |

### Cities
| Endpoint | Description |
|---|---|
| `GET /cities/search` | Search by name. Used in onboarding + profile edit. |
| `GET /cities/{id}` | City detail including active nomad count. |

---

## SignalR Hub — Realtime Events

Hub endpoint: `/hubs/invites`. Client connects on app foreground. Disconnects on background. FCM push handles background.

| Event | Direction | Payload |
|---|---|---|
| `InviteReceived` | Server → receiver | `InviteDto`. Show in-app banner if app is open. |
| `InviteAccepted` | Server → sender | `{ inviteId, acceptedAt }`. Update sent list live. |
| `InviteDeclined` | Server → sender | `{ inviteId }`. Silent — remove from pending list only. |
| `InviteExpired` | Server → both | `{ inviteId }`. Clean up UI without user action. |

---

## Push Notification Templates

All push sent via FCM. Backend stores FCM token per user (updated on each app open via `PATCH /users/me/fcm-token`).

| Trigger | Title | Body |
|---|---|---|
| Invite — standard | `{hangout} at {place}?` | `{senderName} wants to meet you at {placeName}.` |
| Invite — I'm there | `{senderName} is at {place} ☕` | `They're there right now and would love some company.` |
| Invite accepted | `See you there!` | `{receiverName} accepted — they're on their way to {placeName}.` |
| Review prompt | `How did it go?` | `You met {otherName} — let them know how it was.` Sent 3h after invite accepted. |

---

## Build Phases

Six phases. Complete each phase fully before starting the next. **Phases 1–3 are backend-first** so Flutter always has real APIs to call.

---

### Phase 1 — Project scaffolding & infrastructure
> Azure setup · .NET VSA + FastEndpoints · security pipeline · Flutter · CI/CD

| # | Task | Output | Notes |
|---|---|---|---|
| 1.1 | Create Azure resource group. Provision: Container Apps environment, PostgreSQL Flexible Server, Blob Storage, SignalR Service (serverless), AD B2C tenant, Key Vault, Application Insights, CDN profile. | Azure resources live | Enable PostGIS: `CREATE EXTENSION postgis;` |
| 1.2 | Scaffold .NET 8 solution: single `Wander.API` project with VSA folder structure. Folders: `/Features/`, `/Infrastructure/`, `/Common/`. Add NuGet: `FastEndpoints`, `FastEndpoints.Security`, `FastEndpoints.Swagger`, `EF Core + Npgsql`, `Hangfire.AspNetCore + Hangfire.PostgreSql`, `Serilog.AspNetCore + Serilog.Sinks.ApplicationInsights`, `Azure.Storage.Blobs`, `Azure.Identity`, `Microsoft.Azure.SignalR`, `FirebaseAdmin`. | .NET solution compiles | `dotnet new webapi` then restructure |
| 1.3 | Configure security middleware pipeline in `Program.cs` in this exact order: `UseHttpsRedirection` → `UseHsts` → `SecurityHeadersMiddleware` (custom: add X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy) → `UseCors` (strict allow-list, no wildcard) → `UseRateLimiter` → `UseAuthentication` → `UseAuthorization` → `UseFastEndpoints`. Configure all 5 rate limit policies. All exceeded requests → HTTP 429 + `Retry-After`. | Security pipeline active, 429 returns on exceeded limits | Test rate limits with a loop in Postman |
| 1.4 | Create Flutter project (`flutter create wander`). Add packages: `riverpod`, `go_router`, `dio`, `firebase_messaging`, `flutter_secure_storage`, `hive`, `google_maps_flutter`, `image_picker`, `image_cropper`, `signalr_flutter`. | Flutter project runs on simulator | `flutter pub get` |
| 1.5 | Configure Azure AD B2C: sign-up/sign-in user flows, password reset. Register API app + mobile app. Configure MSAL in Flutter (`aad_oauth` package). | Auth flow completes end-to-end | Test with real device |
| 1.6 | Set up GitHub Actions CI/CD: .NET build + test → Docker build → push to Azure Container Registry → deploy to Container Apps. Flutter build for iOS + Android on PR. | Pipeline green on push | |
| 1.7 | Configure Key Vault references in Container Apps. Store all secrets listed in the Environment Variables section. | Secrets resolve in deployed API | Never commit secrets |

---

### Phase 2 — Database schema & core backend
> EF Core migrations · all models · seed data · core APIs

| # | Task | Output | Notes |
|---|---|---|---|
| 2.1 | Implement all EF Core entity classes matching the data models section exactly. Configure relationships, indexes, and PostGIS geography columns in `OnModelCreating`. GiST index on `user.location`. Composite index on `(city_id, is_open_today, last_active_at)`. | All entities configured | Use `HasIndex` for GiST spatial index |
| 2.2 | Run initial migration. Apply to Azure PostgreSQL. Verify PostGIS geography columns created correctly. Seed `HangoutTags` (5 records: coffee, walk, food, explore, cowork) and test cities. | DB schema live on Azure | |
| 2.3 | Implement Auth slices: `/Features/Auth/Register/` and `/Features/Auth/RefreshToken/`. Each slice: Endpoint (`.AllowAnonymous()`, AuthEndpoints rate limit policy), Request record, Response record, Validator. Validate Azure AD B2C token, create user record, return JWT. | Can register + get token | |
| 2.4 | Implement Users slices: GetMe, UpdateMe, GetById, UploadPhoto, DeletePhoto, ToggleOpenToday, AddCity, UpdateCity. Photo upload: generate write-only SAS token scoped to `users/{userId}/photos/` → client uploads direct → client confirms → save CDN URL. | Profile CRUD works | SAS token expiry: 10 minutes |
| 2.5 | Implement Discovery slice: `GET /discover`. PostGIS `ST_DWithin` for city radius (50km). Apply all filters (open today, active within 72h, not invited, not deleted). Cursor pagination. Return `PublicUserDto`. Use Discovery rate limit policy. | Discovery returns real users | Test with 2 accounts |
| 2.6 | Implement Cities slices: SearchCities, GetCity. Implement UserCities (travel history) slices. | City search works | |
| 2.7 | Implement Places slices. Build Google Places API sync Hangfire job — given city + hangout tag, fetch + upsert into Places table. Implement `GET /places/suggest` with sponsored slot logic (sponsored always slot 3). Cache Google API responses 24h. | Place suggestions return 3 results | |
| 2.8 | Write integration tests for all Phase 2 slices using `WebApplicationFactory` + `testcontainers-dotnet` (local PostgreSQL + PostGIS). Minimum: 1 happy path + 1 error case per slice. | All tests green | |

---

### Phase 3 — Invites, meetups & realtime
> Full invite lifecycle · SignalR · FCM push · trust score

| # | Task | Output | Notes |
|---|---|---|---|
| 3.1 | Implement Invites slices: Send, Accept, Decline, GetIncoming, GetSent, GetPast. `POST /invites` uses InviteSend rate limit policy. | Full invite CRUD works | |
| 3.2 | Implement Azure SignalR hub (`Infrastructure/SignalR/InviteHub.cs`). On invite sent → push `InviteReceived` to receiver. On accept → push `InviteAccepted` to sender. On decline → push `InviteDeclined` silently. On expiry → push `InviteExpired` to both. | Realtime events fire in Postman | Use Azure SignalR serverless mode |
| 3.3 | Implement FCM push for all 4 notification templates. Store FCM token on user record (`PATCH /users/me/fcm-token`). Send via Firebase Admin SDK. Exact copy must match Push Notification Templates section. | Push received on real device | Test both iOS + Android |
| 3.4 | Implement Hangfire background jobs: (a) invite expiry — mark expired after 48h, fire `InviteExpired` SignalR event; (b) review prompt — send FCM 3h after accepted; (c) sink inactive — set `is_open_today=false` if `last_active_at > 24h`; (d) Google Places sync. | Jobs run on schedule | Use Hangfire dashboard for monitoring |
| 3.5 | Implement Meetups + Reviews slices. `POST /meetups/{id}/review` triggers server-side trust score recalculation (algorithm in data models section). Increments `wander_meetup_count` on Place. | Review submits, trust score updates | |
| 3.6 | Implement Reports slice. `POST /reports`. Rate limit: Reports (5/day per user). | Report saved | |
| 3.7 | Write integration tests for full invite lifecycle: send → accept → review → trust score updated. | Tests green | |

---

### Phase 4 — Flutter: onboarding & profile
> Auth · onboarding flow · profile screens

| # | Task | Output | Notes |
|---|---|---|---|
| 4.1 | Implement Azure AD B2C auth in Flutter (`aad_oauth`). On first sign-in, call `POST /auth/register`. Store tokens in `flutter_secure_storage`. Implement token refresh interceptor in Dio. | Can sign in on real device | |
| 4.2 | Build onboarding flow (5 screens per wireframes): Welcome → ID Verify → Build Profile → Hangout Tags → Ready. GoRouter onboarding guard — redirects to onboarding if profile incomplete. Progress bar fills across steps. | Full onboarding completes | Persist onboarding step in Hive |
| 4.3 | Integrate Stripe Identity SDK for ID verification step. On success, call backend to mark `is_id_verified=true`. | ID verify completes | Use Stripe Identity Flutter SDK |
| 4.4 | Build profile photo upload flow: `image_picker` → `image_cropper` (square crop) → get SAS URL → upload direct to Azure Blob → confirm to API. | Photos appear in profile | |
| 4.5 | Build profile detail screen (`PublicUserDto`). Match wireframe exactly: hero avatar, stat row (meetups/cities/years/safe rating), trust badges, bio, hangout tags, cities list, reviews (120 char max), sticky CTA button. | Profile screen renders correctly | |
| 4.6 | Build own profile / settings screen: edit bio, manage photos, toggle hangout tags, toggle `open_to_romance`, toggle `open_today`. | Can edit all profile fields | |

---

### Phase 5 — Flutter: core loop
> Discovery · invite flow · places · push notifications

| # | Task | Output | Notes |
|---|---|---|---|
| 5.1 | Build discovery screen. Vertical scrolling card list (not grid, not single swipe). `GET /discover` with city + tag filter. Filter bar (All / Coffee / Walk / Food / Cowork / Explore). Status dot: green = online now / active today, yellow = active recently (1–3 days), nothing = 4+ days. Open Today teal banner. | Discovery shows real users | |
| 5.2 | Build invite flow: hangout type grid (5 types — no date option) → place suggestions (3 cards from `GET /places/suggest`, sponsored label on slot 3 if applicable) → 'I'm already here' toggle → preview bar ('Sara will see: Coffee at Café X?') → send. `POST /invites` on send. | Can send a real invite | |
| 5.3 | Build invites tab: Incoming / Sent / Past sub-tabs. Accept / Not now actions. 'Waiting for reply…' on sent cards (no read receipts). Badge count on tab icon. | Full invite tab works | |
| 5.4 | Connect SignalR hub in Flutter. `InviteReceived` → in-app banner + update incoming tab. `InviteAccepted` → update sent tab. `InviteExpired` → remove from lists. Reconnect on app foreground. | Realtime events update UI | |
| 5.5 | Implement FCM push handling. Deep link: notification tap opens invite detail or review prompt via GoRouter. Test background + killed state. | Push opens correct screen | |
| 5.6 | Build places tab: category filter bar, place rows (name, rating, distance, wifi/quiet pills, meetup count, sponsored pill). `GET /places` for city. | Places tab renders | |
| 5.7 | Build empty state: 'Quiet in [city] today' headline + arriving soon cards (`GET /discover/arriving`). Fallback CTAs: Explore nomad spots + Invite a friend. | Empty state shows correctly | |

---

### Phase 6 — Flutter: post-meetup, polish & launch
> Trust screen · monetisation · testing · App Store / Play Store

| # | Task | Output | Notes |
|---|---|---|---|
| 6.1 | Build post-meetup trust screen. On app open call `GET /meetups/pending-review`. If result exists, show prompt. Check-pills (felt safe / good convo / would meet again), optional text (120 char limit), skip option (one gentle reminder after 24h max). Confirmation screen shows both trust score bars with new segment highlighted. | Full review flow works end-to-end | |
| 6.2 | Implement Nomad Pass subscription: RevenueCat SDK (iOS + Android). `PATCH /users/me` to set `is_premium` flag. Hide ads for premium users. | Subscription purchases work | Use RevenueCat sandbox |
| 6.3 | Implement in-app ads. Google AdMob SDK. Native ad format on discovery screen (sponsored place card slot). Non-premium users only. Max 1 per session. | Sponsored card shows | |
| 6.4 | Full end-to-end QA pass: onboarding → discover → invite → accept → meetup → review → trust score. Real iOS + Android devices. Fix all bugs. | All flows work on real devices | Test both OS versions |
| 6.5 | Performance: lazy load discovery list, cache profile photos, debounce city search, compress images before upload (max 800px, 80% quality). | Discovery scrolls at 60fps | |
| 6.6 | Accessibility: semantic labels on all interactive elements, minimum 44px tap targets, dynamic text size support. | Passes basic a11y audit | |
| 6.7 | App Store + Play Store submission: screenshots (6.5" iPhone, 12.9" iPad, Pixel), descriptions, privacy policy URL, age rating (17+ for location), review notes. | Apps submitted for review | Allow 3–5 days |
| 6.8 | Production checklist: rotate all secrets, enable Azure DDoS protection, configure Container Apps autoscaling rules, set Azure Monitor alerts (error rate > 1%, p99 latency > 2s). | Production verified | |

---

## Environment Variables

All secrets in Azure Key Vault. Referenced as Key Vault references in Container Apps. Local dev: `dotnet user-secrets` or `.env` (gitignored).

```
# .NET API — Azure Key Vault secrets
ConnectionStrings__DefaultConnection   PostgreSQL connection string
AzureAdB2C__ClientId                   AD B2C app client ID
AzureAdB2C__TenantId                   AD B2C tenant ID
AzureAdB2C__PolicyId                   B2C user flow name
AzureSignalR__ConnectionString         SignalR Service connection string
AzureBlob__ConnectionString            Blob Storage connection string
AzureBlob__ContainerName              'wander-photos'
Firebase__ServerKey                    FCM server key
GooglePlaces__ApiKey                   Google Places API key
StripeIdentity__SecretKey             Stripe secret key
ApplicationInsights__ConnectionString  App Insights connection string

# Flutter — .env (gitignored) + dart-define
AZURE_AD_B2C_CLIENT_ID
AZURE_AD_B2C_TENANT_NAME
AZURE_AD_B2C_POLICY_ID
GOOGLE_MAPS_API_KEY_IOS
GOOGLE_MAPS_API_KEY_ANDROID
REVENUECAT_API_KEY_IOS
REVENUECAT_API_KEY_ANDROID
```

---

## Rules for Claude Code

> Read these before writing any code. These are non-negotiable.

- **Follow phases sequentially.** Do not start Phase 3 until Phase 2 tests are green.
- **Use Vertical Slice Architecture throughout.** Each feature is a self-contained folder with Endpoint, Request, Response, Validator. Never share handlers between slices.
- **Use FastEndpoints for all API endpoints.** No controllers. No MediatR. No CQRS overhead. Logic lives directly in `HandleAsync`.
- **Every endpoint must declare its rate limit policy in `Configure()`.** No endpoint may omit rate limiting.
- **Every endpoint must have an `AbstractValidator<TRequest>` in the same slice folder.** Validation runs before `HandleAsync` — never validate inside the handler.
- **Security headers middleware must run on every response.** Never remove `SecurityHeadersMiddleware` from the pipeline.
- **All SAS tokens must be scoped to the requesting user's blob prefix.** A user must never generate a SAS token for another user's prefix.
- **Match data model field names exactly.** API response DTOs must mirror the field names in this document.
- **Never store secrets in code, appsettings.json, or env vars.** Always Azure Key Vault.
- **Never hard-delete user data.** Always soft-delete via `deleted_at`.
- **Discovery endpoint must use PostGIS spatial queries.** Never calculate distance in application code.
- **Trust score must be recalculated server-side on every review submission.** Never trust client-sent scores.
- **No read receipts anywhere.** When an invite is declined, the sender receives no notification — intentional by design.
- **`is_open_to_romance` is a profile field only.** It is never an invite type or hangout tag.
- **Sponsored places must always be slot 3.** Never slot 1 or 2.
- **Push notification copy must match the templates section exactly.**
- **Flutter: use Riverpod `AsyncNotifier` for all state.** No `setState`, no `ChangeNotifier`, no BLoC.
- **Flutter: all screens must match the wireframes** in `wander_product_vision_v1.2.docx`.
- **Write integration tests for every backend slice** before moving to the next phase task.

---

*Wander · Claude Code Build Roadmap v1.1*
