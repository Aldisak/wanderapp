# WanderMeet Frontend — Architecture

Flutter mobile client for the WanderMeet API. Mirrors the backend's vertical-slice philosophy: every feature is a self-contained folder owning its DTOs, repositories, controllers, screens, and route declarations. No horizontal layers (no `services/`, no shared business logic outside `core/`).

This document defines **how** the app is structured. Enforcement lives in `.claude/rules/flutter-architecture.md` (project-specific), `.claude/rules/flutter-style.md`, and `.claude/rules/flutter-naming.md` — those files are the source of truth for the `/fl-feature`, `/fl-tdd`, `/fl-review`, `/fl-debug` skills. The phase plan and UC list live in [`PHASES.md`](./PHASES.md). Per-UC specs live under `docs/specs/`.

---

## 1. Stack

| Concern | Choice | Notes |
|---|---|---|
| Language / SDK | Dart 3.11 / Flutter 3.41 | Already pinned in `pubspec.yaml` |
| Platforms | iOS + Android | No web/desktop — generated `flutter create --platforms ios,android` |
| State management | **Riverpod 3.x with code generation** | `flutter_riverpod 3.2.1`, `riverpod_annotation 4.0.2`, `riverpod_generator 4.0.3`, `riverpod_lint 3.1.3`. AsyncNotifier per slice, providers always generated. |
| Routing | **`go_router`** | Declarative, supports deep links (FCM tap → invite detail). Onboarding + auth guards as redirect callbacks. Named routes only. |
| HTTP client | **`dio`** with interceptors | Auth-token injection, 401 refresh-and-retry, error mapping to typed `AppFailure`. |
| Realtime | **`signalr_netcore`** | Connects to `/hubs/invites` on app foreground, disconnects on background. Forward events through Riverpod streams. |
| Persistence | `flutter_secure_storage` (tokens) + `hive` (typed cache) | Tokens NEVER in `SharedPreferences` / Hive / logs. Hive stores recently-viewed users / places / cities with `version`-tagged adapters. |
| Auth | **Azure AD B2C** via `aad_oauth` | Issued JWT used for both REST and SignalR (`?access_token=` query param for the hub — see backend trap). |
| Maps | `google_maps_flutter` + Google Places API | Place suggest comes from backend; raw Google Places only for map rendering / city centroid. |
| Push | `firebase_messaging` | FCM token PATCHed via `UC-505`. |
| Image | `image_picker` + `image_cropper` | Square crop, max 800 px / 80 % quality before SAS upload. |
| ID verification | `stripe_identity` (Phase 4) | Result confirmed via backend; client never trusts its own success. |
| Subscription | `purchases_flutter` (RevenueCat, Phase 6) | Receipt verified server-side before `is_premium=true`. |
| Ads | `google_mobile_ads` (Phase 6) | Native ad on discovery list, non-premium only, max 1/session. |
| DTO codegen | `freezed` + `json_serializable` | Types are the API contract — owning them by hand is error-prone. |
| Linting | `flutter_lints` + `riverpod_lint` | `riverpod_lint 3.x` runs as analysis-server plugin (already in `analysis_options.yaml`). |

Everything heavier than core lints (Dio interceptors, RevenueCat, Stripe, FCM) is added **only when its phase begins** — see [`PHASES.md`](./PHASES.md). The init commit ships only Riverpod + the generated default app.

---

## 2. Folder layout — vertical slices

The Flutter analogue of `src/WanderMeet.Api/Features/{Area}/{Action}/`. Each slice owns four layers — `data/` (API + DTOs + repository), `domain/` (entities + failures), `application/` (controllers), `presentation/` (screens) — plus a `_routes.dart` file. Cross-feature code lives in `core/` (parallel to backend's `Common/`). No file ever imports across feature boundaries except via `core/` or via routes.

```
lib/
  main.dart                         # ProviderScope → WanderMeetApp
  app/
    wandermeet_app.dart             # MaterialApp.router
    router.dart                     # GoRouter root + redirect guards
    routes.dart                     # AppRoutes / AppRouteNames constants
    theme.dart                      # ThemeData wired to design tokens
    bootstrap.dart                  # Hive init, Firebase init, env loader
  core/                             # cross-cutting infra (mirrors backend Common/)
    network/
      dio_client.dart               # Dio + interceptors + base URL
      auth_interceptor.dart         # injects Bearer token
      refresh_interceptor.dart      # 401 → POST /auth/refresh → retry (single in-flight)
      api_error_interceptor.dart    # maps non-2xx → AppFailure subclass
      pagination.dart               # CursorPage<T>
    auth/
      aad_oauth_client.dart         # AAD B2C wrapper
      token_storage.dart            # flutter_secure_storage facade
      auth_session.dart             # current user + tokens (Riverpod-exposed)
    realtime/
      invite_hub_client.dart        # SignalR connection
      hub_lifecycle.dart            # foreground/background reconnect
    storage/
      hive_boxes.dart               # box names + adapter registration
      hive_typeids.dart             # TypeId registry — single source of truth
    errors/
      app_failure.dart              # sealed AppFailure hierarchy
      api_error_codes.dart          # MIRROR of WanderMeet.Shared.ErrorCodes
      error_messages.dart           # localised user-facing copy per code
    clock/
      clock.dart                    # injected Clock — no DateTime.now() in business logic
    logging/
      logger.dart                   # one shared logger setup
    formatters/                     # distance, duration, relative time
    tokens/
      app_colors.dart               # AppColors.ember (#DC4F2C), teal, iris, sun, neutrals, dark-mode variants
      app_typography.dart           # AppText.* role styles (Newsreader / Manrope / JetBrains Mono via google_fonts)
      app_radius.dart               # AppRadius.xs..pill, AppSpace.xxs..xxxl, AppShadow.*
      app_theme.dart                # AppTheme.light / AppTheme.dark
    widgets/                        # atoms reused across slices
      avatar.dart
      status_dot.dart               # green/yellow/none activity indicator
      open_today_banner.dart
      trust_badge.dart
      hangout_tag_chip.dart
      sponsored_label.dart
      primary_button.dart
      empty_state.dart
  features/                         # vertical slices (1:1 with backend Features/)
    {slice}/
      data/
        {slice}_api.dart            # one method per backend endpoint — returns DTOs
        models/                     # freezed Request / Response / Dto records
        {entity}_repository.dart    # interface
        {entity}_api_repository.dart # impl wrapping {slice}_api + DTO→entity mapping
      domain/
        {entity}.dart               # entity (immutable, freezed or final class)
        {entity}_id.dart            # extension type wrapping String
        {slice}_failures.dart       # sealed extending AppFailure
      application/
        {action}_controller.dart    # @riverpod AsyncNotifier
      presentation/
        {screen}_page.dart
        widgets/{...}.dart
      {slice}_routes.dart           # GoRoute list, exported into lib/app/router.dart
test/
  core/                             # interceptor + dio fakes + hive fakes
  features/{slice}/                 # mirrors lib/features/{slice}/
    application/{controller}_test.dart
    presentation/{screen}_test.dart
    presentation/{screen}_golden_test.dart
    data/{repository}_test.dart
integration_test/
  {uc}_test.dart                    # one per UC happy path
```

**Per-slice rules:**
- One `*_routes.dart` per slice, imported only into `app/router.dart`.
- DTOs in `data/models/` — generated with `freezed` + `json_serializable`. **Never** hand-edit a `*.g.dart` / `*.freezed.dart`.
- Repository pattern is mandatory: controllers depend on the `{Entity}Repository` interface, never on `Dio` or `{Slice}Api` directly. DTO ↔ entity mapping happens in `{entity}_api_repository.dart`, never in widgets.
- Providers are `@riverpod`-annotated — code-generated, never `Provider(...)` ad hoc.
- Screens in `presentation/` consume providers via `ref.watch` / `ref.read`. No HTTP calls inside widgets.
- Sub-views are real `StatelessWidget` types, NOT `Widget _buildX()` methods.
- Cross-feature widget reuse → promote to `core/widgets/`. Cross-feature provider reuse → promote to `core/`.

---

## 3. State management — Riverpod AsyncNotifier per slice

Every screen-driving state is an `AsyncNotifier` (or `AsyncNotifierFamily` when keyed) generated by `riverpod_generator`. The roadmap mandates AsyncNotifier (no `setState`, no `ChangeNotifier`, no BLoC) — this is non-negotiable.

```dart
@riverpod
class DiscoveryFeedController extends _$DiscoveryFeedController {
  @override
  Future<DiscoveryPage> build({required CityId cityId, HangoutTag? tag}) {
    final repo = ref.watch(discoveryRepositoryProvider);
    return repo.fetchPage(cityId: cityId, tag: tag, cursor: null);
  }

  Future<void> loadMore() async { /* paginate by cursor */ }
}
```

**Provider taxonomy (per slice):**
- **API client provider** — `{slice}ApiProvider` wraps the typed `{Slice}Api` class (one method per backend endpoint). Returns DTOs.
- **Repository provider** — `{entity}RepositoryProvider` exposes the `{Entity}Repository` interface, implemented by `{Entity}ApiRepository`. Returns domain entities. Controllers depend on this, never on the API client directly.
- **Read controller** — `AsyncNotifier` exposing the screen's data (e.g. `discoveryFeedControllerProvider`). Calls the repository.
- **Write controller** — `AsyncNotifier` exposing mutator methods (`sendInviteControllerProvider.send(...)`) that perform a mutation, invalidate sibling read controllers when relevant (e.g. invalidate `discoveryFeedControllerProvider` on successful invite).
- **Stream provider** — for SignalR events. `inviteHubStreamProvider` returns `Stream<InviteHubEvent>`; slice controllers `ref.listen` and merge into local state.

**Lifetime:** providers are auto-disposed except `authSessionProvider`, `dioClientProvider`, `tokenStorageProvider`, `inviteHubClientProvider`, repositories, and theme. Use `keepAlive: true` only when state must survive screen pop (e.g. `meControllerProvider` for current user across tabs).

**Code generation:** `dart run build_runner build --delete-conflicting-outputs` after touching any `@riverpod` class. CI runs `dart run build_runner build && git diff --exit-code` to fail on stale generated files.

---

## 4. Networking layer

### 4.1 Dio configuration

A single `Dio` instance is exposed via `dioClientProvider`. Three interceptors, in this exact order:

1. **`AuthInterceptor`** — injects `Authorization: Bearer <accessToken>` from `tokenStorageProvider`. Skips `/auth/register` and `/auth/refresh`.
2. **`RefreshInterceptor`** — on `401`: queue the failed request, call `POST /auth/refresh` with the stored refresh token, persist new tokens via `tokenStorage`, retry the original with the **freshly-stored** access token. Single in-flight refresh — concurrent 401s share the same `Completer<void>`. On refresh failure → clear tokens, dispatch `authSession.signOut()`, GoRouter redirect → `/sign-in`.
3. **`ApiErrorInterceptor`** — converts non-2xx responses to a typed `AppFailure` subclass (`AuthFailure`, `ForbiddenFailure`, `NotFoundFailure`, `ConflictFailure`, `ValidationFailure`, `RateLimitFailure`, `ServerFailure`, `NetworkFailure`). Slice-specific failures (`InviteAlreadyResponded`, `MeetupNotFound`, ...) extend the generic ones with a fixed `code`. Throws an `AppFailureException` so AsyncNotifiers using `AsyncValue.guard` land it as `AsyncError(AppFailure)`.

Base URL comes from `--dart-define=API_BASE_URL=...`. No URLs in source.

### 4.2 Typed API contracts

For each backend slice, one `*_api.dart` file with one method per endpoint. Methods take/return freezed records. The repository wraps the API and maps DTO → entity:

```dart
class InvitesApi {
  InvitesApi(this._dio);
  final Dio _dio;

  Future<SendInviteResponse> send(SendInviteRequest req) async {
    final res = await _dio.post('/api/v1/invites', data: req.toJson());
    return SendInviteResponse.fromJson(res.data as Map<String, Object?>);
  }
}

abstract interface class InviteRepository {
  Future<Invite> send({required UserId receiverId, /* ... */});
}

class InviteApiRepository implements InviteRepository {
  InviteApiRepository(this._api);
  final InvitesApi _api;

  @override
  Future<Invite> send({required UserId receiverId, /* ... */}) async {
    final res = await _api.send(SendInviteRequest(receiverId: receiverId.value, /* ... */));
    return _toDomain(res);
  }

  Invite _toDomain(SendInviteResponse dto) =>
      Invite(id: InviteId(dto.id), /* ... */);
}
```

DTO field names mirror backend records — single source of truth is the FastEndpoints request/response classes. We **don't** generate Dart from OpenAPI on day one (manual contracts are easier to evolve during Phase 4 churn) but we may add `openapi_generator_cli` against the FastEndpoints Swagger doc once contracts stabilise (Phase 6 polish).

### 4.3 Error model — sealed `AppFailure` mirroring backend `ErrorCodes`

`core/errors/app_failure.dart`:

```dart
sealed class AppFailure {
  const AppFailure({required this.code, this.message, this.details});
  final String code;             // mirrors WanderMeet.Shared.ErrorCodes
  final String? message;         // server-provided, for logs
  final Map<String, List<String>>? details;
}

final class NetworkFailure    extends AppFailure { /* 0/timeout/no-internet */ }
final class AuthFailure       extends AppFailure { /* 401 after refresh fails */ }
final class ForbiddenFailure  extends AppFailure { /* 403 */ }
final class NotFoundFailure   extends AppFailure { /* 404 */ }
final class ConflictFailure   extends AppFailure { /* 409 */ }
final class ValidationFailure extends AppFailure { /* 400 + details map */ }
final class RateLimitFailure  extends AppFailure { /* 429 + Retry-After */ }
final class ServerFailure     extends AppFailure { /* 5xx */ }
```

Slice-specific failures live in `domain/{slice}_failures.dart` and extend the generic ones, pinning a code that mirrors `WanderMeet.Shared.ErrorCodes`:

```dart
final class InviteAlreadyResponded extends ConflictFailure {
  const InviteAlreadyResponded({super.message})
      : super(code: ApiErrorCodes.inviteAlreadyResponded);
}
```

`core/errors/api_error_codes.dart` is a **manual mirror** of `src/WanderMeet.Shared/ErrorCodes.cs` — every constant has the same `"Domain.ErrorName"` string. Drift detection: a Phase-6 integration test hits a backend `/api/v1/_diagnostics/error-codes` endpoint (to be added) and asserts the Dart constants match.

UI maps `failure.code` to a localised string via `error_messages.dart`. Unknown code → fall back to `failure.message ?? defaultMessage`. Never raw `${e.toString()}` in UI.

---

## 5. Routing — GoRouter + guards

One `GoRouter` configured in `app/router.dart`. Routes contributed via per-slice `*_routes.dart` files merged into a single `routes:` list. **Named routes only** — string paths used at definition only, navigation uses `context.goNamed(AppRouteNames.inviteDetail, pathParameters: {...})`.

Top-level routes:

```
/sign-in                       (anonymous)
/onboarding/...                (authed but profile incomplete)
/                              (root shell — bottom nav)
  /discover
  /invites/incoming
  /invites/sent
  /invites/past
  /places
  /me
/users/:id                     (public profile)
/invites/:id                   (invite detail — push deep link target)
/invites/compose               (invite send flow shell)
/meetups/:id/review            (post-meetup review prompt)
/settings/...
```

`AppRoutes` constants (paths) and `AppRouteNames` constants (names) live in `lib/app/routes.dart`. No string literals at call sites.

**Redirect guards (single `redirect` callback):**
1. No tokens → `/sign-in` (unless already there).
2. Tokens present but `users/me` returns 404 / `is_onboarded=false` → `/onboarding/welcome`.
3. Onboarding incomplete + path not under `/onboarding` → continue last onboarding step (read from Hive `onboarding_progress` box).
4. All checks pass → allow.

**Deep links:** FCM `data` payload carries `{ "route": "<named-route>", "params": { ... } }`. The push-handler parses and dispatches via `goRouter.goNamed(name, pathParameters: params)`. Cold-start: queue the route until `MaterialApp.router` is built, then dispatch.

---

## 6. Realtime — SignalR `/hubs/invites`

`InviteHubClient` is a singleton (Riverpod `keepAlive: true`) holding a `HubConnection`. Lifecycle:

| App state | Action |
|---|---|
| Foreground (resumed) | Connect if not connected. Subscribe to `InviteReceived`, `InviteAccepted`, `InviteDeclined`, `InviteExpired`. |
| Background (paused) | Disconnect. Backend FCM takes over. |
| Terminated | No connection — FCM only. |

Events are pushed onto a `StreamController<InviteHubEvent>`. The relevant slice providers (`incomingInvites`, `sentInvites`) listen via `ref.listen(inviteHubStreamProvider, (_, ev) => ...)` and patch local state in place — no full refetch.

**Token plumbing:** `aad_oauth` JWT is sent as `?access_token=<jwt>` query param (browsers / Dart can't set headers on the WebSocket upgrade — same constraint as the backend's note in `CLAUDE.md`).

**Test transport:** in widget/integration tests against a fake hub, use `HttpTransportType.LongPolling` (matches the backend test pattern).

---

## 7. Auth flow

```
┌──────────┐    1. aad_oauth.login()     ┌────────────────┐
│ Sign-in  │ ──────────────────────────▶ │  Azure AD B2C  │
│  screen  │ ◀───────────────────────── │  redirects     │
└─────┬────┘    2. id_token + refresh    └────────────────┘
      │
      │ 3. POST /auth/register      (first time only — backend creates User row)
      │    or use the b2c JWT directly on subsequent runs
      ▼
┌──────────────┐   4. Bearer JWT on every call
│  Dio client  │ ─────────────────────────────────▶ /api/v1/*
└──────────────┘
      │ 5. on 401 → POST /auth/refresh → retry original
```

**Token storage:** `flutter_secure_storage` (Keychain / EncryptedSharedPreferences). Tokens are **never** logged, never written to Hive, never sent to crash reporters. On sign-out: clear secure storage, clear Hive boxes, disconnect SignalR, navigate `/sign-in`.

**Soft-deleted user:** backend rejects the JWT with `401`. Refresh-interceptor will fail, triggering forced sign-out. UI shows "Account no longer active" toast then `/sign-in`.

---

## 8. Persistence

| Concern | Storage | Lifetime |
|---|---|---|
| Access + refresh JWT | `flutter_secure_storage` | Until sign-out |
| Onboarding progress (step #) | Hive box `onboarding_progress` | Until onboarding complete |
| Last viewed city | Hive box `app_state` | Forever |
| Recent profile cache | Hive box `profile_cache` (TTL 5 min) | Bounded |
| Discovery feed cursor | In-memory only (Riverpod `keepAlive`) | Until tab unmounts |
| FCM token | Sent to server, not cached | n/a |

**Migration policy:** every Hive adapter has an explicit `typeId` and a `version` column inside the stored object. On version mismatch we drop the box (data is non-critical cache). No silent in-place migrations.

---

## 9. Theming + design tokens

`lib/core/tokens/app_theme.dart` exposes `AppTheme.light` / `AppTheme.dark` — two Material 3 `ThemeData` instances. Brand colour is **`AppColors.ember` (`#DC4F2C`)**, wired as `colorScheme.primary`. All other palette values are derived from this ember seed. For the complete colour table see [`frontend/docs/DESIGN.md §1`](./DESIGN.md).

**Token files** live in `lib/core/tokens/` (four files, four classes):

| File | Class(es) | Contents |
|---|---|---|
| `app_colors.dart` | `AppColors` | Ember family, teal, iris, sun, status dots, neutrals, dark-mode variants, role aliases |
| `app_typography.dart` | `AppText` | Role-based `TextStyle`s (requires `google_fonts ^6.2.1`) |
| `app_radius.dart` | `AppRadius`, `AppSpace`, `AppShadow` | Radii, spacing scale, box-shadow presets |
| `app_theme.dart` | `AppTheme` | `light` + `dark` `ThemeData` |

**Spacing scale** — `AppSpace.xxs / xs / sm / md / lg / xl / xxl / xxxl` → `4 / 6 / 8 / 12 / 16 / 20 / 24 / 32` (default screen padding = `AppSpace.lg`).

**Radii scale** — `AppRadius.xs / sm / md / lg / xl / xxl / pill` → `8 / 10 / 12 / 14 / 16 / 18 / 9999`. Buttons use `md`, inputs `lg`, primary cards `xl`, discovery cards `xxl`.

**Shadows** — `AppShadow.card` (subtle warm lift), `AppShadow.emberCta` (ember glow on primary CTA), `AppShadow.cardDark`.

**Typography** — three font families loaded via `google_fonts ^6.2.1`:
- **Newsreader** (variable serif 300–700, italic) — `displayLarge`, `displayMedium`, `displaySmall`, `displayItalic`, `headline`, `cardName`.
- **Manrope** (humanist sans 200–800) — `title`, `titleSmall`, `body`, `bodySmall`, `caption`, `pill`.
- **JetBrains Mono** — `monoLabel` (11 px), `monoTiny` (9 px) — section taxonomy labels only.

Full type scale table: see [`frontend/docs/DESIGN.md §1`](./DESIGN.md).

**`withOpacity` is banned.** Use `Color.withValues(alpha: 0.xx)` instead (the `withOpacity` API is deprecated in Flutter 3.x and `flutter analyze` will flag it).

**Banned inline literals** (anywhere under `lib/` outside `lib/core/tokens/`):

- `Color(0x...)` literals — use `AppColors.*`
- `EdgeInsets.all(<num literal>)` or `EdgeInsets.symmetric(<num literal>)` — use `AppSpace.*`
- `TextStyle(fontSize: <num literal>)` inline — use `AppText.*`
- `BorderRadius.circular(<num literal>)` — use `AppRadius.*` or the convenience `BorderRadius` constants it exports

`/fl-review` rejects raw values as a blocking finding.

---

## 10. Testing strategy

Three tiers, mirroring backend:

| Tier | What | Tools |
|---|---|---|
| **Unit / provider** | AsyncNotifier behavior, repository mapping, Hive adapters, formatters | `flutter_test` + Riverpod `ProviderContainer` overrides. |
| **Widget** | One per screen — behavior + golden. Provider scope overridden with fakes (`overrideWith((ref) => Fake...)`). | `flutter_test` + `golden_toolkit`. |
| **Integration** | One per UC — real `dio` against a fake HTTP server (`shelf`) returning canned JSON, including SignalR fake hub via `LongPolling` transport. Drives a full happy path. | `integration_test` + `shelf` + custom `FakeInviteHub`. |

**Per-UC test gate:** every shipped UC requires (1) one widget test for the primary screen + (2) one integration test for the happy path. Failure-branch tests are unit tests on the AsyncNotifier — one per `AppFailure` subclass the controller can surface.

**No mocking the network at the Dio level.** Fake at the HTTP/server boundary so interceptors run for real (catches interceptor-order regressions).

**Codegen check:** CI runs `dart run build_runner build --delete-conflicting-outputs` followed by `git diff --exit-code` — same trick as backend's `dotnet format --verify-no-changes`.

**Clock injection:** business logic that depends on time uses an injected `Clock` (`core/clock/clock.dart`). Tests bind to a `FakeClock` pinned to a fixed instant. Direct `DateTime.now()` in business logic is banned.

---

## 11. Security

- Tokens only in `flutter_secure_storage`. Never log, never serialise to Hive, never send to Sentry/Crashlytics.
- TLS-only base URL. `dio` configured with cert pinning in release builds (Phase 6 hardening — sha256 pin of Azure Container Apps cert).
- SAS upload: client receives a short-lived URL **scoped to its own user prefix**; client never asks for another user's prefix. If the backend ever returns a SAS for a different prefix (bug), the upload UI rejects it before sending bytes.
- All secrets injected via `--dart-define=...` at build time (`AZURE_AD_B2C_CLIENT_ID`, `GOOGLE_MAPS_API_KEY_*`, `REVENUECAT_API_KEY_*`). Never in `pubspec.yaml`, never in source. `.env` is gitignored if used for local dev.
- App displays only data the backend returned — `trust_score`, `is_id_verified`, `meetup_count` are read-only. The client never recomputes them.
- Photo upload size capped client-side (max 800 px / 80 % JPEG) before SAS upload — protects user data plan and storage spend.
- Invite-decline silence: the UI must never show "your invite was declined" — the backend doesn't notify, and we don't fabricate the event from client state.

---

## 12. Build, CI, release

| Concern | Choice |
|---|---|
| Local build | `flutter run -d ios` / `flutter run -d android` |
| Codegen | `dart run build_runner watch --delete-conflicting-outputs` during dev |
| CI | GitHub Actions: `flutter analyze`, `flutter test`, `dart run build_runner build && git diff --exit-code`, golden tests on Linux runner |
| Release | Phase 6 — Fastlane lanes per platform; signing keys in 1Password / Azure KV; bumped via `flutter_app_version_management` |
| Versioning | `pubspec.yaml` `version:` field. CI tags releases `v{major}.{minor}.{patch}+{build}`. |

---

## 13. Open questions (decided lazily — not blocking)

- **DTO codegen from OpenAPI** — defer until Phase 6. Hand-rolled freezed types are faster to evolve while contracts churn.
- **Localisation** — English only for MVP. `flutter_localizations` skeleton in Phase 6 if launch markets demand it.
- **Crash reporting** — Sentry vs Firebase Crashlytics. Firebase is already wired (FCM), so default to Crashlytics unless we end up wanting Sentry's release-tracking.
- **In-memory feature flags** — punt entirely. Backend can gate via `is_premium` / `is_id_verified` and we read state from `users/me`.

---

## 14. Source-of-truth files

Authoritative rule files (loaded explicitly by skills via `Required rules`):

- `.claude/rules/flutter-architecture.md` — project-specific WanderMeet conventions; this document's terse partner.
- `.claude/rules/flutter-style.md` — generic Dart/Flutter style.
- `.claude/rules/flutter-naming.md` — generic naming.

Skills:

- `/fl-feature` — scaffold a new vertical slice (data + domain + application + presentation + routes + tests).
- `/fl-tdd` — red-green-refactor TDD cycle.
- `/fl-review` — convention review against the rule files.
- `/fl-debug` — reproduce → isolate → root-cause → fix → regression-test cycle.

Backend rule files referenced for cross-stack consistency (DTO contract, error-code format):

- `.claude/rules/api-design.md`
- `.claude/rules/error-handling.md`
