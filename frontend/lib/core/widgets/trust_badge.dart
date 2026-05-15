import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/tokens/app_radius.dart';
import 'package:wandermeet_app/core/tokens/app_typography.dart';

/// The two trust badge variants.
enum TrustBadgeKind { trusted, idVerified }

/// Trust and ID-Verified pill badge.
/// Both variants use iris color scheme.
/// In dark mode, uses reduced-opacity irisTint background and lighter iris foreground.
class TrustBadge extends StatelessWidget {
  const TrustBadge({super.key, required this.kind, this.score})
    : assert(
        kind != TrustBadgeKind.trusted || score != null,
        'TrustBadge.trusted requires a non-null score',
      );

  final TrustBadgeKind kind;

  /// Required when [kind] == [TrustBadgeKind.trusted].
  final int? score;

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    final bg = isDark
        ? AppColors.irisTint.withValues(alpha: 0.18)
        : AppColors.irisTint;

    final fg = isDark ? AppColors.irisLift : AppColors.iris;

    final label = switch (kind) {
      TrustBadgeKind.trusted => 'Trusted · ${score ?? ''}',
      TrustBadgeKind.idVerified => 'ID Verified',
    };

    final icon = switch (kind) {
      TrustBadgeKind.trusted => Icons.shield_outlined,
      TrustBadgeKind.idVerified => Icons.verified_user_outlined,
    };

    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpace.sm + AppSpace.xxs / 2, // ~10 px ≈ spec 9
        vertical: AppSpace.xxs - AppSpace.xxs / 4, // ~3 px
      ),
      decoration: BoxDecoration(color: bg, borderRadius: AppRadius.pillR),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 11, color: fg),
          const SizedBox(width: AppSpace.xxs + AppSpace.xxs / 4), // ~5 px
          Text(
            label,
            style: AppText.sans(size: 11, weight: FontWeight.w600, color: fg),
          ),
        ],
      ),
    );
  }
}
