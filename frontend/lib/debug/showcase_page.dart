// lib/debug/showcase_page.dart
//
// Design-system showcase — rendered only when kDebugMode is true (via ShowcaseGate).
// No kDebugMode branches inside this file; the gate is the caller's responsibility.
//
// Section isolation: ShowcasePage accepts [showOnly] so golden tests render
// only the section under test, keeping each golden < 10 000 logical px.

import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/tokens/app_radius.dart';
import 'package:wandermeet_app/core/tokens/app_typography.dart';
import 'package:wandermeet_app/core/widgets/avatar.dart';
import 'package:wandermeet_app/core/widgets/discovery_card.dart';
import 'package:wandermeet_app/core/widgets/ember_cta.dart';
import 'package:wandermeet_app/core/widgets/hangout_tag.dart';
import 'package:wandermeet_app/core/widgets/open_today_pill.dart';
import 'package:wandermeet_app/core/widgets/place_mock.dart';
import 'package:wandermeet_app/core/widgets/place_row.dart';
import 'package:wandermeet_app/core/widgets/sponsored_pill.dart';
import 'package:wandermeet_app/core/widgets/status_dot.dart';
import 'package:wandermeet_app/core/widgets/trust_badge.dart';
import 'package:wandermeet_app/core/widgets/user_summary_mock.dart';
import 'package:wandermeet_app/core/widgets/wander_app_bar.dart';
import 'package:wandermeet_app/core/widgets/wander_bottom_nav.dart';
import 'package:wandermeet_app/core/widgets/wander_check_pill.dart';
import 'package:wandermeet_app/core/widgets/wander_mark.dart';
import 'package:wandermeet_app/core/widgets/wander_text_field.dart';
import 'package:wandermeet_app/core/widgets/wander_toggle.dart';

/// Which section(s) to render in [ShowcasePage].
/// Used by golden tests to render only one section at a time.
enum ShowcaseSection { colours, typography, radii, shadows, components, all }

// ─── MOCK DATA ────────────────────────────────────────────────────────────────

const _mockUser = UserSummaryMock(
  firstName: 'Sara',
  flagEmoji: '🇨🇿',
  activity: ActivityStatus.online,
  occupation: 'Product designer',
  daysInCity: 12,
  trustScore: 87,
  isIdVerified: true,
  bio:
      'I move cities every few months and love finding hidden coffee spots and '
      'spontaneous walks through old neighbourhoods.',
  hangouts: [Hangout.coffee, Hangout.walk, Hangout.explore],
  avatarHue: AvatarHue.ember,
);

const _organicPlace = PlaceMock(
  name: 'Café Flore',
  emojiGlyph: '☕',
  rating: 4.7,
  distanceKm: 0.3,
  amenityPills: ['Wifi', 'Power', 'Quiet'],
  meetupCount: 24,
);

const _sponsoredPlace = PlaceMock(
  name: 'The Grand Lobby',
  emojiGlyph: '🏨',
  rating: 4.9,
  distanceKm: 0.8,
  amenityPills: ['Wifi', 'Power', 'Bar'],
  meetupCount: 8,
  isSponsored: true,
  sponsoredPerk: 'Free filter coffee on arrival',
);

// ─── SHOWCASE PAGE ────────────────────────────────────────────────────────────

/// Scrollable design-system showcase page.
///
/// Pass [showOnly] to render only one section — used by golden tests to
/// keep each rendered area under 10 000 logical px.
class ShowcasePage extends StatelessWidget {
  const ShowcasePage({super.key, this.showOnly = ShowcaseSection.all});

  final ShowcaseSection showOnly;

  bool get _showColours =>
      showOnly == ShowcaseSection.all || showOnly == ShowcaseSection.colours;

  bool get _showTypography =>
      showOnly == ShowcaseSection.all || showOnly == ShowcaseSection.typography;

  bool get _showRadii =>
      showOnly == ShowcaseSection.all || showOnly == ShowcaseSection.radii;

  bool get _showShadows =>
      showOnly == ShowcaseSection.all || showOnly == ShowcaseSection.shadows;

  bool get _showComponents =>
      showOnly == ShowcaseSection.all || showOnly == ShowcaseSection.components;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Design System Showcase')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(AppSpace.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (_showColours) ...[
              const _SectionHeader('Colours'),
              const _ColorSwatchesSection(),
              const SizedBox(height: AppSpace.xxl),
            ],
            if (_showTypography) ...[
              const _SectionHeader('Typography'),
              const _TypographySection(),
              const SizedBox(height: AppSpace.xxl),
            ],
            if (_showRadii) ...[
              const _SectionHeader('Radii & spacing'),
              const _RadiiSection(),
              const SizedBox(height: AppSpace.xxl),
            ],
            if (_showShadows) ...[
              const _SectionHeader('Shadows'),
              const _ShadowsSection(),
              const SizedBox(height: AppSpace.xxl),
            ],
            if (_showComponents) ...[
              const _SectionHeader('Components'),
              const _ComponentsSection(),
              const SizedBox(height: AppSpace.xxl),
            ],
          ],
        ),
      ),
    );
  }
}

// ─── SECTION HEADER ──────────────────────────────────────────────────────────

class _SectionHeader extends StatelessWidget {
  const _SectionHeader(this.title);

  final String title;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpace.md),
      child: Text(title.toUpperCase(), style: AppText.monoLabel),
    );
  }
}

// ─── COLOURS SECTION ─────────────────────────────────────────────────────────

class _ColorSwatchesSection extends StatelessWidget {
  const _ColorSwatchesSection();

  static const _swatches = <_SwatchData>[
    // Primary ember
    _SwatchData('ember', AppColors.ember),
    _SwatchData('emberDeep', AppColors.emberDeep),
    _SwatchData('emberLift', AppColors.emberLift),
    _SwatchData('emberTint', AppColors.emberTint),
    // Teal
    _SwatchData('teal', AppColors.teal),
    _SwatchData('tealLift', AppColors.tealLift),
    _SwatchData('tealTint', AppColors.tealTint),
    // Iris
    _SwatchData('iris', AppColors.iris),
    _SwatchData('irisDeep', AppColors.irisDeep),
    _SwatchData('irisTint', AppColors.irisTint),
    _SwatchData('irisLift', AppColors.irisLift),
    // Sun
    _SwatchData('sun', AppColors.sun),
    _SwatchData('sunTint', AppColors.sunTint),
    // Status
    _SwatchData('statusGreen', AppColors.statusGreen),
    _SwatchData('statusYellow', AppColors.statusYellow),
    // Error
    _SwatchData('error', AppColors.error),
    // Earthy neutrals (light)
    _SwatchData('ink', AppColors.ink),
    _SwatchData('ink2', AppColors.ink2),
    _SwatchData('ink3', AppColors.ink3),
    _SwatchData('ink4', AppColors.ink4),
    _SwatchData('line', AppColors.line),
    _SwatchData('paper', AppColors.paper),
    _SwatchData('paper2', AppColors.paper2),
    _SwatchData('white', AppColors.white),
    // Avatar hues
    _SwatchData('clay', AppColors.clay),
    _SwatchData('sand', AppColors.sand),
    _SwatchData('stone', AppColors.stone),
    // Dark mode
    _SwatchData('dBg', AppColors.dBg),
    _SwatchData('dBg2', AppColors.dBg2),
    _SwatchData('dBg3', AppColors.dBg3),
    _SwatchData('dLine', AppColors.dLine),
    _SwatchData('dInk', AppColors.dInk),
    _SwatchData('dInk2', AppColors.dInk2),
    _SwatchData('dInk3', AppColors.dInk3),
  ];

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: AppSpace.md,
      runSpacing: AppSpace.md,
      children: _swatches.map((s) => _ColorSwatch(data: s)).toList(),
    );
  }
}

class _SwatchData {
  const _SwatchData(this.name, this.color);

  final String name;
  final Color color;
}

class _ColorSwatch extends StatelessWidget {
  const _ColorSwatch({required this.data});

  final _SwatchData data;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: AppSpace.xxxl * 3, // 96 px
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: AppSpace.xxxl * 3, // 96
            height: AppSpace.xxxl * 3, // 96
            decoration: BoxDecoration(
              color: data.color,
              borderRadius: AppRadius.all12,
              border: Border.all(color: AppColors.line),
            ),
          ),
          const SizedBox(height: AppSpace.xxs),
          Text(
            data.name,
            style: AppText.monoTiny,
            overflow: TextOverflow.ellipsis,
          ),
        ],
      ),
    );
  }
}

// ─── TYPOGRAPHY SECTION ───────────────────────────────────────────────────────

class _TypographySection extends StatelessWidget {
  const _TypographySection();

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _TypeRow(label: 'displayLarge', style: AppText.displayLarge),
        _TypeRow(label: 'displayMedium', style: AppText.displayMedium),
        _TypeRow(label: 'displaySmall', style: AppText.displaySmall),
        _TypeRow(label: 'displayItalic', style: AppText.displayItalic()),
        _TypeRow(label: 'headline', style: AppText.headline),
        _TypeRow(label: 'cardName', style: AppText.cardName),
        _TypeRow(label: 'title', style: AppText.title),
        _TypeRow(label: 'titleSmall', style: AppText.titleSmall),
        _TypeRow(label: 'body', style: AppText.body),
        _TypeRow(label: 'bodySmall', style: AppText.bodySmall),
        _TypeRow(label: 'caption', style: AppText.caption),
        _TypeRow(label: 'pill', style: AppText.pill),
        _TypeRow(label: 'monoLabel', style: AppText.monoLabel),
        _TypeRow(label: 'monoTiny', style: AppText.monoTiny),
      ],
    );
  }
}

class _TypeRow extends StatelessWidget {
  const _TypeRow({required this.label, required this.style});

  final String label;
  final TextStyle style;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpace.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: AppText.monoTiny.copyWith(color: AppColors.ink3)),
          const SizedBox(height: AppSpace.xxs),
          Text('The quick brown fox', style: style),
        ],
      ),
    );
  }
}

// ─── RADII & SPACING SECTION ──────────────────────────────────────────────────

class _RadiiSection extends StatelessWidget {
  const _RadiiSection();

  static const _radii = <_RadiusSample>[
    _RadiusSample('xs', AppRadius.xs),
    _RadiusSample('sm', AppRadius.sm),
    _RadiusSample('md', AppRadius.md),
    _RadiusSample('lg', AppRadius.lg),
    _RadiusSample('xl', AppRadius.xl),
    _RadiusSample('xxl', AppRadius.xxl),
    _RadiusSample('pill', AppRadius.pill),
  ];

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: AppSpace.md,
      runSpacing: AppSpace.md,
      children: _radii.map((r) => _RadiusTile(sample: r)).toList(),
    );
  }
}

class _RadiusSample {
  const _RadiusSample(this.label, this.value);
  final String label;
  final double value;
}

class _RadiusTile extends StatelessWidget {
  const _RadiusTile({required this.sample});
  final _RadiusSample sample;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final cappedRadius = sample.value > 32 ? 32.0 : sample.value;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Container(
          width: 96,
          height: 64,
          decoration: BoxDecoration(
            color: scheme.surfaceContainerHighest,
            borderRadius: BorderRadius.circular(cappedRadius),
            border: Border.all(color: scheme.outline),
          ),
        ),
        const SizedBox(height: AppSpace.xs),
        Text('AppRadius.${sample.label}', style: AppText.monoLabel),
        Text(
          sample.value > 9000 ? '9999 (pill)' : sample.value.toStringAsFixed(0),
          style: AppText.caption,
        ),
      ],
    );
  }
}

// ─── SHADOWS SECTION ──────────────────────────────────────────────────────────

class _ShadowsSection extends StatelessWidget {
  const _ShadowsSection();

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Wrap(
      spacing: AppSpace.lg,
      runSpacing: AppSpace.lg,
      children: [
        _ShadowTile(
          label: 'AppShadow.card',
          shadow: AppShadow.card,
          surface: scheme.surface,
        ),
        _ShadowTile(
          label: 'AppShadow.emberCta',
          shadow: AppShadow.emberCta,
          surface: AppColors.ember,
        ),
        _ShadowTile(
          label: 'AppShadow.cardDark',
          shadow: AppShadow.cardDark,
          surface: scheme.surface,
        ),
      ],
    );
  }
}

class _ShadowTile extends StatelessWidget {
  const _ShadowTile({
    required this.label,
    required this.shadow,
    required this.surface,
  });

  final String label;
  final List<BoxShadow> shadow;
  final Color surface;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Container(
          width: 120,
          height: 80,
          decoration: BoxDecoration(
            color: surface,
            borderRadius: AppRadius.all16,
            boxShadow: shadow,
          ),
        ),
        const SizedBox(height: AppSpace.sm),
        Text(label, style: AppText.monoLabel),
      ],
    );
  }
}

// ─── COMPONENTS SECTION ───────────────────────────────────────────────────────

class _ComponentsSection extends StatelessWidget {
  const _ComponentsSection();

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        // WanderMark
        _SubHeader('WanderMark'),
        Row(
          children: const [
            WanderMark(size: 24),
            SizedBox(width: AppSpace.lg),
            WanderMark(size: 48),
            SizedBox(width: AppSpace.lg),
            WanderMark(size: 64),
          ],
        ),
        const SizedBox(height: AppSpace.xl),

        // Avatar — all 7 hues
        _SubHeader('Avatar (all 7 hues)'),
        Wrap(
          spacing: AppSpace.md,
          runSpacing: AppSpace.md,
          children: const [
            Avatar(initial: 'A', hue: AvatarHue.ember),
            Avatar(initial: 'B', hue: AvatarHue.iris),
            Avatar(initial: 'C', hue: AvatarHue.teal),
            Avatar(initial: 'D', hue: AvatarHue.clay),
            Avatar(initial: 'E', hue: AvatarHue.sand),
            Avatar(initial: 'F', hue: AvatarHue.stone),
            Avatar(initial: 'G', hue: AvatarHue.emberDeep),
          ],
        ),
        const SizedBox(height: AppSpace.xl),

        // StatusDot
        _SubHeader('StatusDot'),
        Row(
          children: const [
            StatusDot(status: ActivityStatus.online),
            SizedBox(width: AppSpace.lg),
            StatusDot(status: ActivityStatus.recent),
            SizedBox(width: AppSpace.lg),
            StatusDot(status: ActivityStatus.hidden),
          ],
        ),
        const SizedBox(height: AppSpace.xl),

        // OpenTodayPill
        _SubHeader('OpenTodayPill'),
        const OpenTodayPill(),
        const SizedBox(height: AppSpace.xl),

        // HangoutTag — all 5
        _SubHeader('HangoutTag (all 5 hangouts)'),
        Wrap(
          spacing: AppSpace.sm,
          runSpacing: AppSpace.sm,
          children: const [
            HangoutTag(kind: Hangout.coffee),
            HangoutTag(kind: Hangout.walk),
            HangoutTag(kind: Hangout.food),
            HangoutTag(kind: Hangout.explore),
            HangoutTag(kind: Hangout.cowork),
          ],
        ),
        const SizedBox(height: AppSpace.xl),

        // TrustBadge
        _SubHeader('TrustBadge'),
        Wrap(
          spacing: AppSpace.sm,
          runSpacing: AppSpace.sm,
          children: const [
            TrustBadge(kind: TrustBadgeKind.trusted, score: 87),
            TrustBadge(kind: TrustBadgeKind.idVerified),
          ],
        ),
        const SizedBox(height: AppSpace.xl),

        // SponsoredPill
        _SubHeader('SponsoredPill'),
        const SponsoredPill(),
        const SizedBox(height: AppSpace.xl),

        // EmberCTA
        _SubHeader('EmberCTA'),
        EmberCTA(label: "Let's meet", trailingArrow: true, onPressed: () {}),
        const SizedBox(height: AppSpace.md),
        const EmberCTA(label: 'Disabled', onPressed: null),
        const SizedBox(height: AppSpace.xl),

        // WanderTextField
        _SubHeader('WanderTextField'),
        const WanderTextField(hint: 'Find a place'),
        const SizedBox(height: AppSpace.xl),

        // WanderToggle
        _SubHeader('WanderToggle'),
        Row(
          children: const [
            WanderToggle(value: true, onChanged: null),
            SizedBox(width: AppSpace.lg),
            WanderToggle(value: false, onChanged: null),
          ],
        ),
        const SizedBox(height: AppSpace.xl),

        // WanderCheckPill
        _SubHeader('WanderCheckPill'),
        Wrap(
          spacing: AppSpace.sm,
          runSpacing: AppSpace.sm,
          children: const [
            WanderCheckPill(label: 'Felt safe', active: true, onTap: null),
            WanderCheckPill(label: 'Good convo', active: false, onTap: null),
          ],
        ),
        const SizedBox(height: AppSpace.xl),

        // DiscoveryCard
        _SubHeader('DiscoveryCard'),
        DiscoveryCard(user: _mockUser, onTapMeet: () {}),
        const SizedBox(height: AppSpace.xl),

        // PlaceRow — organic + sponsored
        _SubHeader('PlaceRow (organic)'),
        const PlaceRow(place: _organicPlace),
        const SizedBox(height: AppSpace.lg),
        _SubHeader('PlaceRow (sponsored + selected)'),
        const PlaceRow(place: _sponsoredPlace, selected: true),
        const SizedBox(height: AppSpace.xl),

        // WanderAppBar
        _SubHeader('WanderAppBar'),
        const SizedBox(
          height: kToolbarHeight,
          child: WanderAppBar(
            backLabel: 'Today',
            trailing: Icon(Icons.info_outline),
          ),
        ),
        const SizedBox(height: AppSpace.xl),

        // WanderBottomNav
        _SubHeader('WanderBottomNav'),
        WanderBottomNav(
          currentIndex: 0,
          onChange: (_) {},
          incomingUnreadCount: 5,
        ),
        const SizedBox(height: AppSpace.xl),
      ],
    );
  }
}

class _SubHeader extends StatelessWidget {
  const _SubHeader(this.label);

  final String label;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpace.sm),
      child: Text(
        label,
        style: AppText.caption.copyWith(color: AppColors.ink3),
      ),
    );
  }
}
