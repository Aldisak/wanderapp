# WanderMeet Frontend — Phase Plan

Phases 4–6 from `docs/roadmap.md`, decomposed into UC-numbered vertical slices that mirror the backend's UC convention. Each UC ships as one or more work items via the same `designer → design-reviewer → developer → impl-reviewer → quality-gate → commit` pipeline already used for backend UCs.

**Numbering:**
- `UC-4xx` — Phase 4: Foundations + Onboarding + Profile
- `UC-5xx` — Phase 5: Discovery + Invites + Realtime + Push
- `UC-6xx` — Phase 6: Reviews + Monetisation + Polish + Launch

Each UC entry below has: backend dependency, primary screens, providers, key risks, and a one-line test gate. Per-UC specs live under `frontend/docs/specs/{todo,in-progress,done}/` and follow the backend spec format.

**Design source of truth:** [`DESIGN.md`](./DESIGN.md) is the authoritative bridge between the design handoff (`.claude/state/design_handoff_wander_ui/`) and this plan. Every UC below carries a **Design refs** block listing the screen IDs (`S-*`), components, and copy modules it must implement. Tokens (`AppColors / AppText / AppSpace / AppRadius / AppShadow`) and the 13 catalog widgets land via **UC-400** below — every other UC depends on UC-400.

---

## Sequencing rule

Sequential within a phase. **Do not start a Phase 5 UC until all Phase 4 UCs ship and are merged.** Within a phase, UC numerical order is the recommended order, but adjacent UCs may parallelise if dependencies allow (e.g., UC-407 cities depends only on UC-401 auth, not UC-405 public profile).

The backend UC that satisfies each frontend UC's API dependency is listed explicitly. Backend Phase 3 must close (UC-307 FCM push, UC-308 Hangfire jobs, UC-309 e2e test) before the frontend can pass its own e2e gates — but most Phase 4–5 frontend work can proceed in parallel with backend Phase 3 since the relevant backend UCs (201, 204, 205, 301, 302, 304, 305, 306) already shipped.

---

## Phase 4 — Foundations, Onboarding, Profile

> Boots the app from cold install to a fully-onboarded user with photos, bio, hangout tags, and travel history. No discovery, no invites yet.

| UC | Title | Backend dep | Test gate |
|---|---|---|---|
| **UC-400** | **Design System Bootstrap** — copy `flutter_tokens/*.dart` into `lib/core/tokens/`, wire `AppTheme.light/dark` into `MaterialApp.router`, add `assets/mark.svg`, build the 13 catalog widgets, ship the `/_debug/showcase` route (gated behind `kDebugMode`) | n/a — design only | Widget golden of the showcase route locks every colour swatch, every type role, every component visual; `flutter analyze` clean; `dart run build_runner build` produces no diff. |
| **UC-401** | Azure AD B2C sign-in + token storage + Dio refresh interceptor | UC-201 (`/auth/register`, `/auth/refresh`) | Integration: cold-start with no token → sign-in screen → mocked B2C → register call → home shell. |
| **UC-402** | Onboarding flow shell (5 screens, progress bar, GoRouter guard, Hive-persisted progress) | UC-201 + `/users/me` | Widget: progress bar fills 1→5 across screens; killing app between steps resumes at last completed step. |
| **UC-403** | Stripe Identity verification step (UC-402 step 2) | UC-201 + future `PATCH /users/me { isIdVerified }` (verify backend hook exists) | Integration: stripe_identity success → backend confirms → onboarding advances. Failure → can retry. |
| **UC-404** | Profile photo upload — `image_picker` → `image_cropper` → SAS → Azure Blob → confirm | UC-204 (`POST /users/me/photos`, `DELETE /users/me/photos/{id}`) | Integration: pick image → cropped 1:1 → SAS URL fetched → upload to fake Blob → `confirm` POST → photo appears in profile. |
| **UC-405** | Public profile screen (`PublicUserDto`) — hero avatar, stat row, trust badges, bio, hangout tags, cities, reviews, sticky CTA | UC-201 + `GET /users/{id}` + UC-304 (`GET /users/{id}/reviews`) | Widget golden: matches wireframe at iPhone 15 + Pixel 7 sizes. Empty state golden when user has 0 reviews / 0 photos. |
| **UC-406** | Own profile + settings — edit bio, manage photos (reorder/delete), toggle hangout tags, toggle `open_to_romance`, toggle `open_today` | UC-201 + `PATCH /users/me`, `PATCH /users/me/open-today` | Widget: each toggle/edit roundtrips through the controller and reflects after save. |
| **UC-407** | City search + travel history (UserCities) — search by name, add as current/past, set departed_at | UC-201 + `/cities/search`, `/cities/{id}`, `POST /users/me/cities`, `PATCH /users/me/cities/{id}` | Integration: search "Lisb" → tap → POST creates `UserCity` with `arrivedAt=now` → appears in profile cities list. |

**Design refs (Phase 4):**
- **UC-400** — every screen depends on this. Screens: `/_debug/showcase`. Components: all 13 in `DESIGN.md §2`. Tokens: full `AppColors/AppText/AppSpace/AppRadius/AppShadow` from `flutter_tokens/*.dart`. New runtime deps: `google_fonts`, `flutter_svg`, `country_flags`, `cached_network_image`, `intl`.
- **UC-401** — components: `WanderMark`, `EmberCTA`. Copy: `app_copy.dart` buttons + `errors_copy.dart` (`AuthFailure` lines).
- **UC-402** — screens: `S-ONB-1 … S-ONB-5`. Components: `WanderMark`, `EmberCTA`, `WanderTextField`, `Avatar`, `HangoutTag`. Copy: `onboarding_copy.dart`. Progress bar: 3 px `LinearProgressIndicator(color: AppColors.ember, backgroundColor: AppColors.line, minHeight: 3)`.
- **UC-403** — screen: `S-ONB-2`. Iris-tinted promise card with 3 lines from `COPY_DECK.ID_VERIFY_PROMISES`. CTA `"Verify with Stripe →"`.
- **UC-404** — screen: `S-ONB-3` partial. 4-slot avatar uploader (drag-to-reorder, plus on empty), radius `AppRadius.xl`. Copy: `errors_copy.PHOTOS_TOO_FEW`.
- **UC-405** — screen: `S-PROFILE`. Components: `WanderAppBar`, `Avatar 88`, `StatusDot`, `TrustBadge` (trusted + idVerified), `HangoutTag`, `EmberCTA` (sticky bottom). Stat-row card with `meetups / cities / years / felt-safe %`. Section labels: `WHAT WE'D TALK ABOUT`, `OPEN TO`, `CITIES · LAST 18 MONTHS`, `REVIEWS · {count}`. Sticky-CTA gradient mask from `paper.withOpacity(0) → paper`.
- **UC-406** — same screen in edit mode + Settings list. Components: `WanderTextField`, `WanderToggle` (teal track for open-today; default ember track elsewhere). Copy: `SETTINGS_*`, `ROMANCE_TOGGLE_*`, `OPEN_TODAY_*`.
- **UC-407** — city-search sheet + city chips on profile. Current city: teal-dot chip on `tealTint`; other: `paper2` on `line`. Multi-select chip selector during onboarding (UC-402 step 4).

**Phase 4 exit criteria:**
- [ ] UC-400 ships first; the `/_debug/showcase` route renders every token and every catalog widget cleanly.
- [ ] Cold install → sign in → onboard → land on `/discover` (which is empty, but routes correctly).
- [ ] Killing app at any onboarding step resumes correctly.
- [ ] Profile is fully editable, photo upload roundtrips through Blob.
- [ ] All Phase 4 widget + integration tests green.
- [ ] `flutter analyze` clean. `dart run build_runner build` produces no diff.
- [ ] No raw `Color(0x...)`, `EdgeInsets.all(<num>)`, `TextStyle(fontSize: <num>)`, or `BorderRadius.circular(<num>)` outside `lib/core/tokens/`.

---

## Phase 5 — Core loop: Discovery, Invites, Realtime, Push

> The app's primary value loop. User discovers nearby nomads, sends an invite, gets accepted/declined, and SignalR keeps both sides in sync live.

| UC | Title | Backend dep | Test gate |
|---|---|---|---|
| **UC-501** | Discovery feed — vertical scrolling card list, status dots (green/yellow/none), Open Today banner, filter bar (All / Coffee / Walk / Food / Cowork / Explore), cursor pagination | UC-205 (`GET /discover`) | Integration: fetch first page → scroll triggers next-cursor → filter switch invalidates feed → empty city renders empty state from UC-508. |
| **UC-502** | Invite send flow — hangout type grid (5) → place suggestions (3 cards from suggest, slot-3 may be sponsored) → "I'm already here" toggle → preview bar → send | UC-301 (`POST /invites`) + Places suggest | Integration: full happy path send → returns 201 with InviteDto → appears in Sent tab. |
| **UC-503** | Invites tab — Incoming / Sent / Past sub-tabs, accept / "not now" buttons on incoming, "Waiting for reply…" on sent (no read receipts), past list with statuses, badge count on tab icon | UC-301 (`PATCH /invites/{id}/accept`, `/decline`, `GET /invites/incoming|sent|past`) | Integration: accept incoming → moves to Past with `accepted` status; decline silently removes (no notification). |
| **UC-504** | SignalR realtime wiring — connect on foreground, disconnect on background, `InviteReceived` shows in-app banner + updates Incoming, `InviteAccepted` updates Sent live, `InviteExpired` removes from lists | UC-306 (`/hubs/invites`) | Integration: fake hub emits `InviteReceived` while on Discovery → banner shows + Incoming badge increments without manual refresh. |
| **UC-505** | FCM device token registration — request perms, retrieve token, `PATCH /users/me/fcm-token` on app open and on token refresh | UC-305 (`PATCH /users/me/fcm-token`) | Unit: token rotation triggers PATCH; app open with same token does NOT spam PATCH (debounced via Hive timestamp). |
| **UC-506** | FCM push handling — foreground: in-app banner; background: system tray; killed: cold start with route. Deep link map: `route` → GoRouter, copy must match the 4 backend templates exactly | UC-307 (FCM push service) | Integration on real device: tap of "{hangout} at {place}?" notification cold-starts to `/invites/:id`. |
| **UC-507** | Places tab — category filter bar, place rows (name, rating, distance, wifi/quiet pills, meetup count, sponsored pill), `GET /places` for current city, place detail screen with map | Places list + GetPlace | Widget golden: matches wireframe; sponsored row's pill renders with sponsor copy. |
| **UC-508** | Empty state — "Quiet in [city] today" + "Arriving Soon" cards from `/discover/arriving`, fallback CTAs (Explore nomad spots → Places tab; Invite a friend → share sheet) | UC-205 (`GET /discover/arriving`) | Widget: when discovery returns 0 results, empty state shows; tapping "Arriving soon" card opens public profile. |

**Design refs (Phase 5):**
- **UC-501** — screen: `S-TODAY` (minus empty-state, which is UC-508). Components: `WanderMark`, `Avatar`, `StatusDot`, `OpenTodayPill`, `OpenTodayBanner` (header), hangout filter pills (ChoiceChips; active `ink`/`paper`), `DiscoveryCard`, `WanderBottomNav` (index 0). Hero hero hero: `"{count} nomads"` newline italic-ember `"open today."` Skeleton state: 3 shimmering `paper2` placeholder cards. Copy: `OPEN_TODAY_ON`, `OPEN_TODAY_OFF`, `DISCOVERY_NARROW`, `PRIMARY_CTA = "Let's meet"`.
- **UC-502** — screen: `S-INVITE-COMPOSE`. Components: `WanderAppBar` (Cancel / "Invite {name}" / spacer), recipient strip (`Avatar 44` + flag + `StatusDot` + `TrustBadge.trusted`), hangout 5-grid (selected = ember bg / white icon), `PlaceRow` ×3 (slot-2 is sponsored if `place.isSponsored`; FIRST place selected by default with trailing ember check chip), `WanderToggle` "I'm at {place} right now ☕" (ember track), preview card on `irisTint` with monoLabel "SARA WILL SEE" + italic 24/serif title in ember, sticky `EmberCTA "Send invite"`. Copy: `SEND_INVITE`, `SARA WILL SEE` (recipient uppercased).
- **UC-503** — screen: `S-INVITES-TAB`. SegmentedButton: Incoming / Sent / Past. Incoming card: `Avatar 48` + monoLabel `"RECEIVED {x} HOURS AGO"` + inline `PlaceRow` mini + `EmberCTA` 44-h + `OutlinedButton "Not now"`. Sent card: monoLabel `"WAITING FOR REPLY"` (never "Seen"/"Read"). Past card: status label `Accepted/Declined/Expired/Met`. Tab badge: ember (not red), only when `incomingUnread > 0`. Copy: `ACCEPT`, `ON_MY_WAY`, `NOT_NOW`, `INVITES_EMPTY`.
- **UC-504** — in-app banner overlay on `S-TODAY`. Banner widget reuses `Avatar` + `HangoutTag` + push titles for copy. Updates flow into `inviteHubStreamProvider` → slice controllers patch local state in place.
- **UC-505** — no new screen; system permission prompt. PATCH `/users/me/fcm-token` debounced via Hive `app_state` last-seen timestamp.
- **UC-506** — deep link map. Push templates and `wander://` schemes pinned in `push_copy.dart` and `SCREEN_SPECS.md §9`. INVITE_DECLINED has NO push — the client must not synthesise one.
- **UC-507** — screen: `S-PLACES`. Components: `WanderMark` 26, search field (`AppRadius.lg`, surface bg, 1 px line), filter pills (All / Cafés / Cowork / Food / Parks / Explore), `PlaceRow` list with `SponsoredPill` where applicable, `WanderBottomNav` (index 1). Hero `"Nomad-rated"` + italic-ember `"places"` + ` in {city}.`
- **UC-508** — screen: `S-EMPTY-DISCOVERY` rendered inside `S-TODAY` when list is empty. Components: monoLabel `"ARRIVING SOON"` + mini cards (`Avatar 36` + name + flag + arrival date), two `OutlinedButton.icon` fallbacks. Copy: `DISCOVERY_EMPTY.head`, `DISCOVERY_EMPTY.body`. Never ghost profiles, never "be the first!".

**Phase 5 exit criteria:**
- [ ] Two physical-device test accounts: A sends invite → B accepts → both see status updates live via SignalR.
- [ ] FCM push received on both iOS + Android, tap deep-links into the right screen from cold start.
- [ ] Discovery feed scrolls at 60 fps with 100+ cards loaded.
- [ ] Decline path emits zero push and no in-app banner for the sender.
- [ ] All Phase 5 widget + integration tests green.

---

## Phase 6 — Reviews, monetisation, polish, launch

> Closes the meetup loop with a review prompt and trust score, adds monetisation, hardens for store submission.

| UC | Title | Backend dep | Test gate |
|---|---|---|---|
| **UC-601** | Post-meetup review prompt — on app open, `GET /meetups/pending-review`; if exists, modal with check-pills (felt safe / good convo / would meet again), optional 120-char text, skip option (one gentle reminder allowed via UC-307 review-prompt push) | UC-302 (`GET /meetups/pending-review`, `POST /meetups/{id}/review`) | Integration: pending review present → modal shows on app open → submit → trust score on user's profile increments. |
| **UC-602** | Review confirmation screen — both trust score bars (reviewer + reviewee) with new segment animated in | UC-302 server-side recomputed score | Widget: animation runs once, before/after bar widths match incoming `trust_score_delta`. |
| **UC-603** | Public reviews on profile — already wired in UC-405; this UC adds the "see all reviews" sheet with full list pagination | UC-304 (`GET /users/{id}/reviews`) | Integration: load 50+ reviews via cursor pagination, scroll renders all without jank. |
| **UC-604** | Nomad Pass subscription — RevenueCat SDK, paywall screen, restore purchases, `PATCH /users/me { isPremium }` after server-verified receipt | future `PATCH /users/me { isPremium }` (ensure backend hook exists, may need a small backend UC) | Integration with sandbox: purchase succeeds → paywall closes → `is_premium=true` reflected. |
| **UC-605** | Report user flow — report sheet on public profile, max 300-char reason field, `POST /reports`, surface 429 if quota hit | UC-303 (`POST /reports`) | Unit: 6th report on same day surfaces 429 toast with retry-after copy. |
| **UC-606** | Performance polish — lazy-load discovery list, cache profile photos via `cached_network_image`, debounce city search (300 ms), pre-upload image compression (already in UC-404, audit here) | n/a | Manual: discovery scroll at 60 fps with 200+ cards on a Pixel 4a. |
| **UC-607** | Accessibility pass — semantic labels on all interactive elements, minimum 44 px tap targets, dynamic text-size support, color-blind audit on status dots | n/a | Tooling: `flutter test --machine` accessibility checks; manual VoiceOver + TalkBack run on representative screens. |
| **UC-608** | Store submission + production checklist — App Store + Play Store metadata, screenshots (6.5" iPhone, 12.9" iPad, Pixel), privacy policy, age rating 17+, signing keys rotated, cert pin enabled in release builds | n/a | Manual: TestFlight + Internal Testing track green, then submit. |

**Design refs (Phase 6):**
- **UC-601** — screen: `S-REVIEW` (modal). Hero: monoLabel `"THREE HOURS AFTER {PLACE}"` + Newsreader 34 `"How did it go"` + italic-ember `"with {otherName}?"` Helper line about the silent feedback. Step 01: Y/N two-button row. Step 02: 3× `WanderCheckPill` (Felt safe / Good convo / Would meet again) — disabled (opacity 0.4 + `IgnorePointer`) when `didMeet != yes`. Step 03: 120-char `TextField` with monoLabel counter. Sticky `EmberCTA "Confirm"`. Copy: `CONFIRM`, `SKIP_NOW`, section labels `"01 · DID YOU MEET UP?"`, `"02 · WHAT WAS TRUE?"`, `"03 · SHORT REVIEW · OPTIONAL · 120"`.
- **UC-602** — both-bars trust-animation screen. Bar widths from `trust_score_delta`; animation runs once.
- **UC-603** — paginated "see all reviews" sheet under `S-PROFILE`. Review card: 3 teal-tint check pills at top + italic 14/400 body + footer `"— {reviewerFirstName}. · met in {city}"`. Pagination: 10 at a time.
- **UC-604** — paywall screen. `EmberCTA` + iris-tinted promise card. Copy TBD; follow voice rules in `COPY_DECK.md`.
- **UC-605** — report sheet on `S-PROFILE`. `WanderTextField` (300 char limit). Copy: button is `"Report a concern"`, never "Report user".
- **UC-606** — perf polish. No new screens. Validates discovery 60 fps target from Phase 5.
- **UC-607** — accessibility pass. Semantic labels, 44 × 44 tap targets, dynamic text-size support, status-dot redundant cues (hue + shape) audit.
- **UC-608** — store submission. Screenshots derived from real screens (`S-TODAY`, `S-PROFILE`, `S-INVITE-COMPOSE`, `S-PLACES`, `S-REVIEW`).

**Phase 6 exit criteria:**
- [ ] End-to-end happy path on real iOS + Android: install → onboard → discover → invite → meet → review → trust score increments.
- [ ] Subscription works in sandbox.
- [ ] Accessibility audit passes basic checks.
- [ ] App submitted to both stores.

---

## Design non-negotiables (apply to every UC)

Carried from `DESIGN.md §6`. `/fl-review` rejects deviations.

1. **No swipe mechanic.** Discovery is a vertical list.
2. **No red notification badges.** Use `ember` if a badge is needed.
3. **No match percentages or compatibility scores.** Anywhere. Ever.
4. **No precise last-seen timestamps.** Only the 3-state activity system, hue **and** shape (filled vs hollow).
5. **No "X viewed your profile."** Paid feature only on Nomad Pass profile-view list.
6. **No streaks. No re-engagement guilt nags.**
7. **`is_open_to_romance` is a profile field**, not a hangout type.
8. **Sponsored** appears only at slot-3 of invite suggestions (UC-502). Places tab (UC-507) may have sponsored rows anywhere but always with the same `SponsoredPill`. Never a banner.
9. **Invite declined → silent.** UC-506 emits zero push. UI must not fabricate the event.
10. **First names only** everywhere. Never "user", never "@handle".
11. **Iris** is reserved for Trust + ID Verified. **Teal** is reserved for Open Today + active-today dot. **Sun** is reserved for Sponsored.

---

## What goes into a frontend UC spec

Same shape as backend specs:

```
docs/specs/todo/UC-401_signin_and_token_lifecycle.md
docs/specs/todo/UC-401_signin_and_token_lifecycle-work-items.md  (after designer pass)
```

Each spec:
1. **Goal** — one paragraph, user-facing outcome.
2. **Backend dependency** — exact endpoint(s) and any DTO links.
3. **Screens** — wireframe references + states (loading / empty / error).
4. **Providers** — list of Riverpod providers introduced and their `keepAlive` posture.
5. **Routes** — new GoRouter routes + redirect implications.
6. **Test gates** — concrete widget test + integration test acceptance criteria.
7. **Non-goals** — explicit list of what this UC does NOT include.

The `designer` agent splits a spec into work items. The `developer` agent implements one work item via TDD. The `impl-reviewer` agent gates the commit. Same pipeline as backend.

---

## Tracking

A new memory entry (`project_frontend_phase.md`) records the current frontend-phase state. Update it after each UC ships — same convention as `project_phase3_backlog.md`.
