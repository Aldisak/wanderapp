import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/tokens/app_radius.dart';
import 'package:wandermeet_app/core/tokens/app_typography.dart';

/// "SPONSORED" stamp on slot-3 places. Never anywhere else.
/// Background sunTint, foreground darkened sun for AA on tint.
/// Always displays the literal string "Sponsored".
class SponsoredPill extends StatelessWidget {
  const SponsoredPill({super.key});

  static const _kHPad = AppSpace.xxs + AppSpace.xxs - AppSpace.xxs / 4; // ~7 px
  static const _kVPad = AppSpace.xxs / 2; // 2 px

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: _kHPad, vertical: _kVPad),
      decoration: const BoxDecoration(
        color: AppColors.sponsoredBg,
        borderRadius: AppRadius.pillR,
      ),
      child: Text(
        'Sponsored'.toUpperCase(),
        style: AppText.monoTiny.copyWith(
          color: AppColors.sponsoredFg,
          letterSpacing: 0.10 * 9,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}
