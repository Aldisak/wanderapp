import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/tokens/app_radius.dart';
import 'package:wandermeet_app/core/tokens/app_typography.dart';
import 'package:wandermeet_app/core/widgets/avatar.dart';
import 'package:wandermeet_app/core/widgets/ember_cta.dart';
import 'package:wandermeet_app/core/widgets/hangout_tag.dart';
import 'package:wandermeet_app/core/widgets/status_dot.dart';
import 'package:wandermeet_app/core/widgets/trust_badge.dart';
import 'package:wandermeet_app/core/widgets/user_summary_mock.dart';

/// Discovery card shown in the Today feed.
/// Tap → push ProfileScreen(userId) (UC-405 wires the navigation;
/// WI-3 exposes the [onTap] callback only).
class DiscoveryCard extends StatelessWidget {
  const DiscoveryCard({
    super.key,
    required this.user,
    required this.onTapMeet,
    this.onTap,
  });

  final UserSummaryMock user;
  final VoidCallback onTapMeet;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: AppRadius.all18,
          border: Border.all(color: AppColors.line),
          boxShadow: AppShadow.card,
        ),
        padding: const EdgeInsets.all(AppSpace.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _IdentityRow(user: user),
            const SizedBox(height: AppSpace.md),
            _BioText(bio: user.bio),
            const SizedBox(height: AppSpace.md),
            _HangoutsRow(hangouts: user.hangouts),
            if (user.activity != ActivityStatus.hidden) ...[
              const SizedBox(height: AppSpace.md),
              _CtaRow(activity: user.activity, onTapMeet: onTapMeet),
            ],
          ],
        ),
      ),
    );
  }
}

class _IdentityRow extends StatelessWidget {
  const _IdentityRow({required this.user});

  final UserSummaryMock user;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Avatar(
          initial: user.firstName,
          size: 56,
          hue: user.avatarHue,
          imageUrl: user.imageUrl,
        ),
        const SizedBox(width: AppSpace.md),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Text(user.firstName, style: AppText.cardName),
                  const SizedBox(width: AppSpace.xxs),
                  Text(user.flagEmoji, style: const TextStyle(fontSize: 16)),
                  const SizedBox(width: AppSpace.xxs),
                  StatusDot(status: user.activity),
                ],
              ),
              const SizedBox(height: AppSpace.xxs),
              Row(
                children: [
                  Text(user.occupation, style: AppText.caption),
                  Text(
                    ' · ${user.daysInCity} days here',
                    style: AppText.caption,
                  ),
                ],
              ),
            ],
          ),
        ),
        const SizedBox(width: AppSpace.sm),
        Column(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            TrustBadge(kind: TrustBadgeKind.trusted, score: user.trustScore),
            if (user.isIdVerified) ...[
              const SizedBox(height: AppSpace.xxs),
              const TrustBadge(kind: TrustBadgeKind.idVerified),
            ],
          ],
        ),
      ],
    );
  }
}

class _BioText extends StatelessWidget {
  const _BioText({required this.bio});

  final String bio;

  @override
  Widget build(BuildContext context) {
    return Text(
      bio,
      style: AppText.bodySmall,
      maxLines: 2,
      overflow: TextOverflow.ellipsis,
    );
  }
}

class _HangoutsRow extends StatelessWidget {
  const _HangoutsRow({required this.hangouts});

  final List<Hangout> hangouts;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: AppSpace.xs,
      runSpacing: AppSpace.xs,
      children: hangouts.map((h) => HangoutTag(kind: h)).toList(),
    );
  }
}

class _CtaRow extends StatelessWidget {
  const _CtaRow({required this.activity, required this.onTapMeet});

  final ActivityStatus activity;
  final VoidCallback onTapMeet;

  @override
  Widget build(BuildContext context) {
    final cta = EmberCTA(
      label: "Let's meet",
      onPressed: onTapMeet,
      trailingArrow: true,
    );

    if (activity == ActivityStatus.recent) {
      return Opacity(opacity: 0.85, child: cta);
    }
    return cta;
  }
}
