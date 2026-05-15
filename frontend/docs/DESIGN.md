# WanderMeet Frontend — Design Assignment

This document is the **bridge** between the raw design handoff (`.claude/state/design_handoff_wander_ui/`) and the engineering pipeline (`PHASES.md` + `ARCHITECTURE.md` + `.claude/rules/flutter-*.md`). It captures the design system, components, copy, and screen specs that the upcoming UCs MUST implement. If a behaviour differs between the handoff HTML/JSX and the markdown specs, **the markdown specs win** — the HTML is a snapshot, the markdown is the source of truth.

The product is now branded **Wander**. Tagline: *Meet someone real, today, wherever you are.* The frontend project directory stays `frontend/` for now; the Dart `pubspec.yaml` `name:` is still `wandermeet_app` and need not be renamed until launch.

---

## 0. Sources (read first)

| File | Purpose |
|---|---|
| `.claude/state/design_handoff_wander_ui/README.md` | Overview + non-negotiables + open questions |
| `.claude/state/design_handoff_wander_ui/COMPONENT_SPECS.md` | 13 reusable widgets — build these first |
| `.claude/state/design_handoff_wander_ui/SCREEN_SPECS.md` | Screen widget trees + data dependencies |
| `.claude/state/design_handoff_wander_ui/COPY_DECK.md` | Every user-facing string, frozen |
| `.claude/state/design_handoff_wander_ui/flutter_tokens/*.dart` | Drop-in colour / type / radius / theme files |
| `.claude/state/design_handoff_wander_ui/design_reference/Wander Brand Book.html` | Open in browser for visual gestalt |

**Fidelity:** high. Pixel-match the screens. Spacing tokens are exact and are the only values used.

---

## 1. Token system (replaces the placeholder in `ARCHITECTURE.md §9`)

The placeholder `AppColors.brand = #534AB7` and `s4/s8/s16/...` token names in `flutter-architecture.md` are **superseded**. Drop the four files under `flutter_tokens/` into `lib/core/tokens/` verbatim and import everywhere via:

```dart
import 'package:wandermeet_frontend/core/tokens/app_colors.dart';
import 'package:wandermeet_frontend/core/tokens/app_typography.dart';
import 'package:wandermeet_frontend/core/tokens/app_radius.dart'; // also exports AppSpace, AppShadow
import 'package:wandermeet_frontend/core/tokens/app_theme.dart';
```

### 1.1 Colours — `AppColors`

| Token | Hex | Use |
|---|---|---|
| `ember` | `#DC4F2C` | **Primary** — every CTA, link, focus ring |
| `emberDeep` | `#8E2710` | Pressed states, brand on dark |
| `emberLift` | `#F07B5A` | Dark-mode primary |
| `emberTint` | `#FBE3DA` | Soft fills, hover, hangout-pill background |
| `teal` | `#1F9985` | **Reserved for "Open today"** and the green-status dot. Never primary. |
| `tealLift` | `#4FBFAC` | Dark-mode teal |
| `tealTint` | `#DCEFEB` | Open Today banner background, current-city chip |
| `iris` | `#6B4FB8` | **Reserved for Trust + ID Verified**. Nothing else. |
| `irisDeep` | `#3F2D7A` | Preview card on dark |
| `irisTint` | `#EEEAFA` | Trust-badge background, invite preview card |
| `sun` / `sunTint` | `#E8A33A` / `#FBEDD2` | **Sponsored pill only**, slot-3 of invite suggestions |
| `statusGreen` | `#3DA869` | Active-today dot (filled) |
| `statusYellow` | `#D4A53C` | Active-recently dot (hollow ring) |
| `error` | `#B84545` | Administrative errors only — never social |
| `ink` / `ink2` / `ink3` / `ink4` | `#1F1A1A` / `#3F3633` / `#7A6E69` / `#B0A49E` | Body / secondary / caption / disabled |
| `line` | `#E6DECF` | Borders, hairlines |
| `paper` | `#F5EEDF` | Screen background |
| `paper2` | `#FBF6EA` | Raised surface, tinted sections |
| `white` | `#FFFFFF` | Card surface (light mode) |
| `dBg` / `dBg2` / `dBg3` / `dLine` / `dInk` / `dInk2` / `dInk3` | per `app_colors.dart` | Dark mode neutrals |

**Role aliases** also defined: `primary = ember`, `primaryOn = white`, `surface = white`, `background = paper`, `textPrimary = ink`, `trustBadgeFg = iris`, `openTodayFg = teal`, `sponsoredFg = #9C7019` (darkened sun for AA on tint).

### 1.2 Type — `AppText` (depends on `google_fonts: ^6.2.1`)

Three families:

- **Newsreader** (variable serif, 300–700, italic supported) — display, headline, card-name, italic ember accents.
- **Manrope** (humanist sans, 200–800) — title, body, caption, pill.
- **JetBrains Mono** — `monoLabel` (11 px) + `monoTiny` (9 px) for section taxonomy labels only.

Role styles: `displayLarge / displayMedium / displaySmall / displayItalic / headline / cardName / title / titleSmall / body / bodySmall / caption / pill / monoLabel / monoTiny`. Inline `TextStyle(fontSize: ...)` is banned everywhere except the token file itself.

### 1.3 Spacing, radii, shadows

- `AppSpace.xxs/xs/sm/md/lg/xl/xxl/xxxl` → `4 / 6 / 8 / 12 / 16 / 20 / 24 / 32` (default screen padding = `lg`).
- `AppRadius.xs/sm/md/lg/xl/xxl/pill` → `8 / 10 / 12 / 14 / 16 / 18 / 9999`. Buttons `md`, inputs `lg`, primary cards `xl`, discovery cards `xxl`.
- `AppShadow.card` (subtle warm), `AppShadow.emberCta` (ember glow on primary CTA), `AppShadow.cardDark`.

The old generic `s4/s8/s16/...` and `r4/r8/r16` names from `flutter-architecture.md` are **retired**. Any existing reference must be updated.

### 1.4 Theme

`AppTheme.light` / `AppTheme.dark` are the single Material 3 `ThemeData`s. They wire ember as `colorScheme.primary`, teal as `secondary`, iris as `tertiary`. ElevatedButton defaults to 54-h filled ember with `AppRadius.lg`. AppBar is paper with 0 elevation. Page transitions are Cupertino on both platforms.

### 1.5 Brand mark

`WanderMark` is the SVG logo. Build with `flutter_svg` from `assets/mark.svg`. Below 32 px → render monochrome (drop the teal seed); ≥ 32 px → render the seed.

### 1.6 Required pubspec additions on top of UC-401

```yaml
dependencies:
  google_fonts: ^6.2.1
  flutter_svg: ^2.0.10
  country_flags: ^3.0.0
  cached_network_image: ^3.3.1
  intl: ^0.19.0
```

`country_flags` per the design handoff's open question — default to system emoji rendering for v1 and only swap in the package if emoji rendering proves inconsistent across platforms.

---

## 2. Component catalog (lives in `lib/core/widgets/`)

These 13 widgets are **cross-slice** and must be implemented in the order listed (each unlocks the next). They live under `lib/core/widgets/` and are the only widgets `features/*` may import from `core`. Per-slice widgets stay under `features/{slice}/presentation/widgets/`.

| # | Widget | File | Notes |
|---|---|---|---|
| 1 | `WanderMark` | `core/widgets/wander_mark.dart` | SVG-backed; size/foreground/seed |
| 2 | `Avatar` | `core/widgets/avatar.dart` | 7 hues; image when present else initial; `AvatarHue` enum |
| 3 | `StatusDot` | `core/widgets/status_dot.dart` | `ActivityStatus { online, recent, hidden }`; **never colour alone** |
| 4 | `OpenTodayPill` | `core/widgets/open_today_pill.dart` | Teal-only mini pill |
| 5 | `TrustBadge` | `core/widgets/trust_badge.dart` | `TrustBadgeKind { trusted, idVerified }` |
| 6 | `HangoutTag` | `core/widgets/hangout_tag.dart` | `Hangout { coffee, walk, food, explore, cowork }` |
| 7 | `SponsoredPill` | `core/widgets/sponsored_pill.dart` | Slot-3 of invite suggestions ONLY |
| 8 | `DiscoveryCard` | `core/widgets/discovery_card.dart` | The Today-feed card; composes 1, 2, 3, 5, 6 |
| 9 | `PlaceRow` | `core/widgets/place_row.dart` | Used on Places tab and invite composer |
| 10 | `WanderBottomNav` | `core/widgets/wander_bottom_nav.dart` | Custom (BackdropFilter); active item ember |
| 11 | `WanderAppBar` + `WanderBackButton` | `core/widgets/wander_app_bar.dart` | Minimal back chevron + Newsreader label |
| 12 | `WanderTextField` + `WanderToggle` + `WanderCheckPill` | `core/widgets/form_primitives.dart` | Filled surface inputs; Cupertino-style toggle |
| 13 | `EmberCTA` | `core/widgets/ember_cta.dart` | Wraps `ElevatedButton` with arrow logic + pressed-scale |

Cross-slice widget reuse is mandatory: e.g. `DiscoveryCard` is used by UC-501 AND inside UC-508's empty-state-on-discovery, so it lives in `core/widgets/`, not `features/discovery/`.

**Hangout icons** (`IconCoffee`, `IconWalk`, `IconFood`, `IconExplore`, `IconCowork`) recreate from `wm-graphics.jsx`. 24-grid, 1.75 px stroke, 2 px corner radius. Implement as small `CustomPainter` classes under `core/widgets/icons/`.

---

## 3. Screen catalog

Each screen below maps to one or more existing UCs in `PHASES.md`. The mapping is in §5 of this doc.

| ID | Screen | Source spec |
|---|---|---|
| S-TODAY | Today / Discovery | `SCREEN_SPECS.md §1` |
| S-PROFILE | Profile detail | `SCREEN_SPECS.md §2` |
| S-INVITE-COMPOSE | Invite composer | `SCREEN_SPECS.md §3` |
| S-REVIEW | Post-meetup review | `SCREEN_SPECS.md §4` |
| S-PLACES | Places tab | `SCREEN_SPECS.md §5` |
| S-ONB-1 .. S-ONB-5 | Onboarding (Welcome / ID verify / Profile / Tags + cities / Ready) | `SCREEN_SPECS.md §6` |
| S-EMPTY-DISCOVERY | Empty state (quiet city) | `SCREEN_SPECS.md §7` |
| S-INVITES-TAB | Invites tab (Incoming / Sent / Past) | `SCREEN_SPECS.md §8` |

Push-notification copy is canonical in `SCREEN_SPECS.md §9` and `COPY_DECK.md`.

---

## 4. Copy binding

All user-facing strings come from `COPY_DECK.md`. The current architecture targets English-only for MVP (per `ARCHITECTURE.md §13`), so the copy lives as `static const` on a typed class under `lib/core/copy/` (one file per surface):

```
lib/core/copy/
  app_copy.dart            // global ‘house rules’ + buttons + section labels
  push_copy.dart           // 5 push templates + deeplink paths
  onboarding_copy.dart     // step heroes + sub-copy + CTAs
  errors_copy.dart         // 6 user-voice error lines
  empty_states_copy.dart   // discovery, invites, profile, etc.
```

When `flutter_localizations` is wired in Phase 6, these constants migrate to ARB files unchanged — the **keys** are stable, the **values** move.

**Load-bearing labels (never paraphrase):**
- Bio label is `"What we'd talk about"`, **not** "About me" / "Bio" / "About".
- Decline button is `"Not now"`, **not** "Decline".
- Report button is `"Report a concern"`, **not** "Report user".
- "Sponsored" pill label is literally `"Sponsored"`, **not** "Ad" / "Promoted".
- Open Today off-state: `"You're not visible today. Tap to open."`

---

## 5. UC mapping (extends `PHASES.md`)

Per-UC design references. Phase 4–6 UC numbering and ordering from `PHASES.md` is unchanged — this table layers design IDs on top.

### Phase 4

| UC | Screens | Components | Copy modules |
|---|---|---|---|
| **UC-400** (new — Design System Bootstrap) | Theme showcase route `/_debug/showcase` | All 13 catalog components | `app_copy.dart` skeleton |
| **UC-401** Sign-in + token lifecycle | (auth handled outside named screens; tokens, refresh) | `WanderMark`, `EmberCTA` | `app_copy.dart` buttons; `errors_copy.dart` for AuthFailure |
| **UC-402** Onboarding shell | S-ONB-1 → S-ONB-5 with PageController (no swipe), 3 px ember progress bar | `WanderMark`, `EmberCTA`, `WanderTextField`, `Avatar`, `HangoutTag` | `onboarding_copy.dart` |
| **UC-403** Stripe ID verify | S-ONB-2 (step 02 of onboarding) | `EmberCTA`, iris-tinted promise card | `onboarding_copy.ID_VERIFY_*` |
| **UC-404** Photo upload | S-ONB-3 partial (avatar uploader 4 slots) | `Avatar`, `WanderTextField` | `errors_copy.PHOTOS_TOO_FEW` |
| **UC-405** Public profile | S-PROFILE | `WanderAppBar`, `Avatar`, `StatusDot`, `TrustBadge`, `HangoutTag`, `EmberCTA`, stat-row card, city chips, review cards | `app_copy.SECTION_LABELS`, `PROFILE_NO_BIO` |
| **UC-406** Own profile + settings | (S-PROFILE in edit mode + settings list) | `WanderTextField`, `WanderToggle`, `HangoutTag` | `app_copy.SETTINGS_*`, `ROMANCE_TOGGLE_*`, `OPEN_TODAY_*` |
| **UC-407** Cities + travel history | (city search sheet + city chips on profile) | search field, city chips | — |

### Phase 5

| UC | Screens | Components | Copy modules |
|---|---|---|---|
| **UC-501** Discovery feed | S-TODAY (excluding empty-state, which is UC-508) | `WanderMark`, `OpenTodayBanner`, `WanderToggle`, hangout filter pills, `DiscoveryCard`, `WanderBottomNav` | `app_copy.OPEN_TODAY_*`, `DISCOVERY_NARROW`, `PRIMARY_CTA` |
| **UC-502** Invite send | S-INVITE-COMPOSE | `WanderAppBar`, recipient strip (`Avatar`+`TrustBadge`), hangout 5-grid, `PlaceRow`, `SponsoredPill`, `WanderToggle`, preview card, `EmberCTA` | `app_copy.SEND_INVITE`, `SECTION_LABELS["SARA WILL SEE"]` |
| **UC-503** Invites tab | S-INVITES-TAB (Incoming / Sent / Past) | `Avatar`, `StatusDot`, `PlaceRow`, `EmberCTA` (small variant), `OutlinedButton` "Not now" | `app_copy.ACCEPT`, `NOT_NOW`, `INVITES_EMPTY` |
| **UC-504** SignalR realtime | (in-app banner overlay on S-TODAY + live updates to S-INVITES-TAB) | small banner widget (uses `Avatar`, `HangoutTag`) | push titles re-used as banner copy |
| **UC-505** FCM token register | (no new screen — permission prompt + silent PATCH) | — | — |
| **UC-506** FCM push handling | (deep-link routes — no new screens) | — | `push_copy.dart` (5 templates) |
| **UC-507** Places tab | S-PLACES | `WanderMark`, search field, filter pills, `PlaceRow`, `SponsoredPill`, `WanderBottomNav` | section heroes |
| **UC-508** Empty discovery | S-EMPTY-DISCOVERY | `Avatar` 36, mini-cards, `OutlinedButton` fallback CTAs | `empty_states_copy.DISCOVERY_EMPTY`, `ARRIVING_SOON` |

### Phase 6

| UC | Screens | Components | Copy modules |
|---|---|---|---|
| **UC-601** Review prompt | S-REVIEW modal | `WanderCheckPill`, `WanderTextField` (120 char counter), `EmberCTA` | `app_copy.CONFIRM`, `SKIP_NOW`, `SECTION_LABELS["01..03"]` |
| **UC-602** Review confirmation | Both-bars trust animation | trust-bar widget | — |
| **UC-603** See-all-reviews sheet | (paginated reviews list under S-PROFILE) | review cards | — |
| **UC-604** Nomad Pass subscription | Paywall screen | `EmberCTA`, iris-tinted promise card | TBD (write in Phase 6) |
| **UC-605** Report user | Report sheet on S-PROFILE | `WanderTextField` (300 char) | `app_copy.REPORT` |
| **UC-606** Perf polish | (no new screens) | — | — |
| **UC-607** Accessibility pass | (no new screens) | semantic labels everywhere | — |
| **UC-608** Store submission | (assets only) | — | — |

---

## 6. Non-negotiables (carry into every UC review)

From `README.md` of the handoff. These are **product constants** — `/fl-review` rejects any deviation.

1. **No swipe mechanic.** Discovery is a vertical list.
2. **No red notification badges.** Use `ember` if a badge is needed. None on Discovery.
3. **No match percentages or compatibility scores.** Anywhere. Ever.
4. **No precise last-seen timestamps.** Only the 3-state activity system (online / recent / hidden), with hue **and** shape (filled vs hollow).
5. **No "X viewed your profile."** Paid-feature only on Nomad Pass profile-view list (still gentle).
6. **No streaks. No re-engagement guilt nags.** No "Where have you been?" copy.
7. **`is_open_to_romance` is a profile field**, not a hangout type. Toggle in Settings.
8. **Sponsored places** appear only at slot-3 in invite suggestions. Use `SponsoredPill`, never a banner. The Places tab is a different surface — sponsored rows can appear anywhere there, but always with the same pill.
9. **Invite declined → silent.** Sender gets no notification. UI must not fabricate the event.
10. **First names only** everywhere. Never "user", never "@handle".
11. **Iris** is reserved for Trust + ID Verified. **Teal** is reserved for Open Today + active-today dot. **Sun** is reserved for Sponsored. Bleeding any of these into other surfaces is a review block.

---

## 7. Accessibility floor

- Tap targets ≥ 44 × 44 pt (Material 48 acceptable).
- Text ≥ 13 pt for ambient, ≥ 16 pt for body.
- The 3-state status dot ships **two redundant cues** — hue and shape. Never hue alone.
- Colour-blind safety: no red in social surfaces. Error red is admin-only.
- All ember-on-paper button text passes AA (4.5:1). White-on-ember passes AA and AAA at large.
- VoiceOver / TalkBack: every interactive element labelled.

---

## 8. Implications for in-flight work

### UC-400 — new prerequisite UC

Insert a new UC at the head of Phase 4: **UC-400 Design System Bootstrap**. Deliverables:

- Copy the four `flutter_tokens/*.dart` files into `lib/core/tokens/`.
- Add `google_fonts`, `flutter_svg`, `cached_network_image`, `intl`, `country_flags` to `pubspec.yaml`.
- Wire `MaterialApp(theme: AppTheme.light, darkTheme: AppTheme.dark, themeMode: ThemeMode.system)` — `MaterialApp.router` lands in UC-401.
- Build a `/_debug/showcase` route (release builds: gated behind a `kDebugMode` check) that renders every token and every catalog widget — used by `/fl-review` to spot regressions.
- Add `assets/mark.svg` and register it in `pubspec.yaml`.

Test gate: showcase route boots cleanly on iOS + Android, `flutter analyze` clean, golden test of showcase route locks all token colour swatches.

### UC-401 conflict

The current UC-401 work-items (`frontend/docs/specs/in-progress/signin-and-token-lifecycle-work-items.md`) plan a token system that no longer matches the design:

- WI-1 sets `AppColors.brand = Color(0xFF534AB7)` → must become `AppColors.ember = Color(0xFFDC4F2C)` (and rely on `AppColors.primary = ember` as the role alias used by widgets).
- WI-1 spacing tokens `s4/s8/s16/...` → renamed to `xxs/xs/sm/md/lg/xl/xxl/xxxl`.
- WI-1 radii `r4/r8/r16` → renamed to `xs/sm/md/lg/xl/xxl/pill`.
- WI-1 text styles (`display/headline/body/caption`) → replaced by the role styles in `app_typography.dart`, which require the `google_fonts` dependency.
- WI-1 currently bundles all token work. After inserting UC-400, **WI-1 of UC-401 should be reduced** to: bootstrap shell, redacting logger, AppRoutes/AppRouteNames constants — token wiring moves to UC-400.

The conflict resolution is a user decision (see §10).

### Architecture + rule-file follow-up

`frontend/docs/ARCHITECTURE.md §9` ("Theming + design tokens") and `.claude/rules/flutter-architecture.md` ("Theming — design tokens") still reference the old token names and `#534AB7` brand. They must be updated in lock-step with UC-400 landing. The replacement copy is §1 of this document.

---

## 9. Open questions (from the handoff)

These were flagged by the designer; resolutions live with the developer:

1. **Country flags** — system emoji vs `country_flags` package. **Default: system emoji** for v1. Switch to `country_flags` only if rendering proves inconsistent across iOS + Android.
2. **Avatar fallback** — no photo → `Avatar` with first initial; hue from `userId.hashCode % 7` (7 hues defined in `AvatarHue`).
3. **Status bar style** — dark icons on paper, light on dark mode. Set via `SystemUiOverlayStyle` in `bootstrap.dart`.
4. **Map view** — not in MVP. Bottom nav is `Today / Places / Invites / You`. **The arch's `/discover` name should be aliased to S-TODAY** but the route path stays `/discover` for symmetry with the backend endpoint.

---

## 10. Decision points for the user

Before continuing UC-401 implementation under the new design:

1. **Insert UC-400 ahead of UC-401?** Recommended. Lets the design system land independently and unblocks every other UC.
2. **Patch UC-401 in place vs. re-design?** If UC-400 lands, UC-401's WI-1 shrinks dramatically and may not need a fresh designer pass. If we skip UC-400 and fold the design system into UC-401's WI-1 instead, the design-review for UC-401 must re-run.
3. **Rename `lib/core/tokens/` files to match handoff filenames?** Handoff ships `app_colors.dart / app_typography.dart / app_radius.dart / app_theme.dart`. The architecture mentions `app_colors.dart / app_text_styles.dart / app_spacing.dart / app_radii.dart`. Recommended: adopt the handoff names verbatim (they're shorter and the source files map 1:1).

These decisions are captured here so the conductor pipeline knows when to resume.
