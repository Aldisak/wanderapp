{
  "$schema": ".claude/schemas/spec.v1.json",
  "uc_id": "UC-400",
  "slug": "design-system-bootstrap",
  "title": "Design system bootstrap — tokens, theme, the 13-widget catalog, and a debug showcase route",
  "actors": [
    "Cold-installed mobile app developer pulling the repo for the first time — every screen must compose from the catalog widgets they find under lib/core/widgets/.",
    "/fl-review and /fl-design-reviewer agents — both reject deviation from the token contract once UC-400 lands.",
    "Designer (Claude Code) — the showcase route is the visual regression target; every component lands in the golden test bound to that route."
  ],
  "preconditions": [
    "The design handoff at .claude/state/design_handoff_wander_ui/ is present and authoritative (README.md, COMPONENT_SPECS.md, SCREEN_SPECS.md, COPY_DECK.md, flutter_tokens/*.dart). frontend/docs/DESIGN.md is the integration bridge derived from it.",
    "The Flutter project at frontend/ exists with flutter 3.41.9 / dart 3.5.x and Riverpod 3.x codegen pinned (flutter_riverpod 3.2.1 + riverpod_annotation 4.0.2 + riverpod_generator 4.0.3 + riverpod_lint 3.1.3). flutter analyze + flutter test are clean before this UC starts.",
    "No feature slices exist yet — UC-401, UC-402, etc. are all in docs/specs/todo/. UC-400 is the new head of Phase 4 and blocks every later UC.",
    "The Flutter assets/ directory does not yet contain the Wander mark SVG. UC-400 introduces assets/mark.svg derived from the spec in COMPONENT_SPECS.md §1 (two crescents with a teal seed)."
  ],
  "main_flow": [
    "Developer adds runtime deps to frontend/pubspec.yaml: google_fonts ^6.2.1, flutter_svg ^2.0.10, country_flags ^3.0.0, cached_network_image ^3.3.1, intl ^0.19.0. (Auth/HTTP/realtime/persistence deps belong to UC-401 and remain absent here.)",
    "Developer copies the four files from .claude/state/design_handoff_wander_ui/flutter_tokens/ into frontend/lib/core/tokens/ verbatim: app_colors.dart, app_typography.dart, app_radius.dart (exports AppSpace + AppShadow), app_theme.dart. Each file's header comment is preserved. No content edits — these are the source of truth.",
    "Developer adds assets/mark.svg under frontend/assets/ — the Wander logo per COMPONENT_SPECS.md §1 spec (two circles radius 42% canvas, centres (36%, 50%) and (64%, 50%), each masked by inner circle radius 78% offset 46% inward, seed circle 4.5% canvas centred). Registers it in pubspec.yaml under flutter.assets.",
    "Developer rewrites frontend/lib/main.dart: WidgetsFlutterBinding.ensureInitialized() → runApp(ProviderScope(child: WanderMeetApp())). The previous counter sample is gone.",
    "Developer creates frontend/lib/app/wandermeet_app.dart: const ConsumerWidget that returns MaterialApp(theme: AppTheme.light, darkTheme: AppTheme.dark, themeMode: ThemeMode.system, home: ShowcaseGate()). (Real GoRouter wiring lands in UC-401.)",
    "Developer creates frontend/lib/app/showcase_gate.dart: in kDebugMode renders the showcase route; in release renders a tiny 'Wander — design system loaded' placeholder so production builds still link the file cleanly.",
    "Developer implements the 13 catalog widgets under frontend/lib/core/widgets/ in the order from COMPONENT_SPECS.md §checklist: wander_mark.dart, avatar.dart (+ AvatarHue enum), status_dot.dart (+ ActivityStatus enum), open_today_pill.dart, hangout_tag.dart (+ Hangout enum + per-hangout IconCoffee/IconWalk/IconFood/IconExplore/IconCowork CustomPainters under widgets/icons/), trust_badge.dart (+ TrustBadgeKind enum), sponsored_pill.dart, discovery_card.dart (composes Avatar+StatusDot+TrustBadge+HangoutTag+EmberCTA — accepts a UserSummary mock type defined inline under core/widgets/discovery_card_models.dart until UC-405 introduces the real model), place_row.dart (accepts a Place mock type under core/widgets/place_row_models.dart), wander_bottom_nav.dart (custom Container with BackdropFilter — not Material BottomNavigationBar), wander_app_bar.dart (+ WanderBackButton, implements PreferredSizeWidget), form_primitives.dart (WanderTextField + WanderToggle + WanderCheckPill), ember_cta.dart (wraps ElevatedButton with optional trailing arrow and pressed-scale 0.98 animation 100 ms).",
    "Developer creates frontend/lib/debug/showcase_page.dart: a scrollable page that renders every token swatch (colour grid, type-role samples, radius samples, shadow samples) and every catalog widget in its primary states. Gated by kDebugMode; never reachable in release. This page is the golden-test target.",
    "Developer updates frontend/docs/ARCHITECTURE.md §9 (Theming + design tokens) to replace the placeholder AppColors.brand = #534AB7 + s4/s8/.../r4/r8/r16 description with the new contract — short paragraph plus a link to frontend/docs/DESIGN.md §1 for the full table. The 'no inline Color(0x...)' rule list updates to reference AppColors.ember + AppSpace.lg + AppRadius.lg as the canonical replacements.",
    "Developer updates .claude/rules/flutter-architecture.md 'Theming — design tokens' section to mirror the ARCHITECTURE.md change: ember as brand (#DC4F2C), new token names (AppColors / AppText / AppSpace.xxs..xxxl / AppRadius.xs..pill / AppShadow), three-family typography requirement (google_fonts dep), 11 non-negotiables block linking to PHASES.md.",
    "Developer runs dart run build_runner build --delete-conflicting-outputs to regenerate Riverpod / freezed sources (the catalog widgets are not themselves @riverpod, but the codegen check must pass with zero diff). Then flutter analyze clean, flutter test clean."
  ],
  "alternate_flows": [
    "google_fonts cannot resolve Newsreader / Manrope / JetBrainsMono at runtime (offline first launch on CI runner) → app falls back to platform default fonts; the showcase page still renders but the golden test is marked as 'expected baseline online'. Workaround: pre-cache fonts via GoogleFonts.config.allowRuntimeFetching = false + bundled assets if launch markets demand offline. Out of scope for UC-400.",
    "flutter_svg cannot parse assets/mark.svg (developer's SVG is malformed) → showcase page shows a 24×24 placeholder square in AppColors.ember to make the failure visible. WanderMark exposes a fallback widget for this case.",
    "country_flags is unused in UC-400 itself but is added now so UC-405 (profile screen, with the country flag emoji on the name row) lands without a separate dep PR. If the package is removed later because system emoji proves sufficient, UC-400 keeps the import out of any committed code so removal is purely a pubspec edit.",
    "Showcase page is too tall to render in a single widget golden (>10k logical px) → splitting into sections is acceptable: showcase_colors_golden_test, showcase_typography_golden_test, showcase_components_golden_test. Each golden is a separate file under test/debug/."
  ],
  "acceptance_criteria": [
    "frontend/pubspec.yaml lists exactly these new runtime deps (in addition to the existing flutter + riverpod stack): google_fonts ^6.2.1, flutter_svg ^2.0.10, country_flags ^3.0.0, cached_network_image ^3.3.1, intl ^0.19.0. No other deps are added by UC-400. (HTTP/auth/realtime deps remain absent — those belong to UC-401.)",
    "frontend/pubspec.lock is regenerated and committed. Versions are pinned to the resolver's selection.",
    "frontend/lib/core/tokens/ contains four files copied verbatim from .claude/state/design_handoff_wander_ui/flutter_tokens/: app_colors.dart, app_typography.dart, app_radius.dart, app_theme.dart. md5sum of each lib file matches the handoff source.",
    "frontend/lib/main.dart contains only: WidgetsFlutterBinding.ensureInitialized(); runApp(const ProviderScope(child: WanderMeetApp())); — no counter sample, no business logic.",
    "frontend/lib/app/wandermeet_app.dart wires MaterialApp(theme: AppTheme.light, darkTheme: AppTheme.dark, themeMode: ThemeMode.system). The home is ShowcaseGate (kDebugMode → ShowcasePage, release → tiny Text placeholder).",
    "frontend/assets/mark.svg exists and renders as a 24×24 / 80×80 SVG via flutter_svg without parse errors. The asset is registered under pubspec.yaml's flutter.assets list.",
    "All 13 catalog widgets are implemented under frontend/lib/core/widgets/ (plus widgets/icons/ for the 5 hangout glyphs). Each is a const-constructible StatelessWidget with all fields final. Tap targets ≥ 44 × 44 logical px (Material 48 acceptable). No raw Color(0x...), EdgeInsets.all(<num>), TextStyle(fontSize: <num>), or BorderRadius.circular(<num>) anywhere in core/widgets/ — every visual constant comes from AppColors / AppText / AppSpace / AppRadius / AppShadow.",
    "ShowcasePage renders every AppColors token in a labelled swatch grid (24 colours minimum), every AppText role in a labelled sample row, every AppRadius value as a sample container, every AppShadow value as a sample card, and every catalog widget in its primary state (Avatar in all 7 hues, StatusDot in all 3 states, TrustBadge in both kinds, HangoutTag for all 5 hangouts, SponsoredPill, DiscoveryCard with mock UserSummary, PlaceRow with mock Place, WanderBottomNav, WanderAppBar, EmberCTA filled + outlined).",
    "frontend/test/debug/ contains at least three golden tests (showcase_colors_golden_test, showcase_typography_golden_test, showcase_components_golden_test) — they snapshot the showcase page on a fixed Pixel 4a resolution (1080×2340 logical px or scaled down to fit the golden target) and lock every token + every widget visual.",
    "frontend/test/core/widgets/ contains per-widget behaviour tests (one file per catalog widget) that assert: const constructors compile, every visible label/icon is reachable, semantic labels are present on interactive widgets (EmberCTA / WanderToggle / WanderCheckPill / WanderBottomNav), StatusDot for ActivityStatus.recent renders a transparent fill + non-zero border (the redundant-cue contract from DESIGN.md §7).",
    "frontend/docs/ARCHITECTURE.md §9 is updated: the placeholder AppColors.brand = #534AB7 / AppSpacing.s4/s8/... / AppRadii.r4/r8/r16 description is replaced with the ember-led token contract. A 2-line link points to frontend/docs/DESIGN.md §1 for the full table. The banned-inline-literals list explicitly references the new token class names.",
    ".claude/rules/flutter-architecture.md is updated in the 'Theming — design tokens' section: brand colour is now #DC4F2C ember, token class names are AppColors / AppText / AppSpace / AppRadius / AppShadow, the three-family typography requirement (google_fonts) is called out, and a 'Non-negotiables (from DESIGN.md §6)' block lists the 11 product constants — these are now part of /fl-review's rejection contract.",
    "flutter analyze is clean. flutter test (including the new golden tests) is clean. dart run build_runner build --delete-conflicting-outputs produces zero diff against the working tree. No new warnings, no deprecated API uses introduced.",
    "Conventional Commit on each work item — see PHASES.md guidance. The series ends with a single 'Add /_debug/showcase route' commit OR the showcase page is part of the WI that introduces it; either is acceptable as long as the WI boundary is clean.",
    "After UC-400 ships, UC-401 spec (currently in docs/specs/todo/) can be re-run through the designer with the new token contract in effect. UC-401's WI-1 shrinks: tokens + theme + showcase route are gone from its scope; bootstrap shell, redacting logger, and AppRoutes/AppRouteNames constants remain."
  ],
  "out_of_scope": [
    "GoRouter wiring beyond the placeholder home: WanderMeetApp uses MaterialApp (not MaterialApp.router) in this UC. UC-401 introduces the real GoRouter and redirect guards.",
    "Any HTTP client, Dio, interceptors, token storage, auth-session state. UC-401's territory.",
    "Any feature slice (auth/discovery/invites/...). UC-401 is the first feature slice.",
    "Localisation — copy_deck strings land as const Dart strings under lib/core/copy/ in the UC that needs them (e.g. push templates in UC-506, onboarding strings in UC-402). UC-400 does NOT introduce flutter_localizations.",
    "Animation polish — the EmberCTA pressed-scale 0.98 is part of UC-400; everything else (StatusDot pulse, banner slide-in, trust-bar animation) lands with the UC that uses it.",
    "Image-handling deps — image_picker / image_cropper land with UC-404. cached_network_image is added in UC-400 because catalog widgets use it for Avatar's image branch.",
    "DiscoveryCard's real UserSummary / Place data types — UC-400 ships mock model types under core/widgets/{discovery_card_models,place_row_models}.dart for the showcase page. Real DTOs land with UC-405 (profile/users) and UC-507 (places) and the mocks are deleted then. This is explicitly an intentional short-term mock: tagged with a TODO comment naming the UC that replaces it."
  ],
  "non_functional": [
    "Performance: cold launch to ShowcasePage rendered on a Pixel 4a target < 2.0 s (release build, fonts cached). google_fonts runtime download is best-effort on first launch — fallback fonts render immediately.",
    "Accessibility floor (DESIGN.md §7): tap targets ≥ 44 × 44, text ≥ 13 pt ambient / ≥ 16 pt body, StatusDot redundant cue (hue + shape), every ember-on-paper / white-on-ember combo passes WCAG AA (4.5:1). Audited in the showcase golden + per-widget behaviour tests.",
    "Codegen freshness gate: CI runs dart run build_runner build --delete-conflicting-outputs && git diff --exit-code as part of the impl-reviewer phase. Stale generated files block merge.",
    "Theming purity: zero raw Color/EdgeInsets/TextStyle/BorderRadius literals in lib/ outside lib/core/tokens/. /fl-review must reject deviations once UC-400 lands.",
    "Asset weight: assets/mark.svg target < 3 KB (it's two crescents + a circle — well under).",
    "No silent fallbacks for missing tokens: if a widget references AppColors.foo that doesn't exist, the build fails. This is enforced by static typing — token classes have no dynamic lookup.",
    "Light + dark mode both render every catalog widget correctly. The showcase golden runs at brightness: Brightness.light AND brightness: Brightness.dark — two separate goldens per section."
  ]
}
