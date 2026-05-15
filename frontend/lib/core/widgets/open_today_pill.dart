import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/tokens/app_radius.dart';
import 'package:wandermeet_app/core/tokens/app_typography.dart';

/// Teal "I'm open today" pill. Reserved for one purpose only.
class OpenTodayPill extends StatelessWidget {
  const OpenTodayPill({super.key});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpace.xs + AppSpace.xxs, // 6 + 4 = 10 px
        vertical: AppSpace.xxs, // 4 px
      ),
      decoration: const BoxDecoration(
        color: AppColors.teal,
        borderRadius: AppRadius.pillR,
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 5,
            height: 5,
            decoration: const BoxDecoration(
              color: AppColors.white,
              shape: BoxShape.circle,
            ),
          ),
          const SizedBox(width: AppSpace.xxs),
          Text(
            'Open today',
            style: AppText.pill.copyWith(color: AppColors.white),
          ),
        ],
      ),
    );
  }
}
