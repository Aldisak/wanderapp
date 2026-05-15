import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/tokens/app_radius.dart';
import 'package:wandermeet_app/core/tokens/app_typography.dart';
import 'package:wandermeet_app/core/widgets/icons/icon_coffee.dart';
import 'package:wandermeet_app/core/widgets/icons/icon_cowork.dart';
import 'package:wandermeet_app/core/widgets/icons/icon_explore.dart';
import 'package:wandermeet_app/core/widgets/icons/icon_food.dart';
import 'package:wandermeet_app/core/widgets/icons/icon_walk.dart';

/// Hangout type enum.
enum Hangout { coffee, walk, food, explore, cowork }

/// Pill identifying a hangout type.
/// Background emberTint, text emberDeep, icon ember.
/// Spec: horizontal padding 9, vertical 4, icon 12 px, 5 px gap, label 11/500.
class HangoutTag extends StatelessWidget {
  const HangoutTag({super.key, required this.kind});

  final Hangout kind;

  static const _kHPad =
      AppSpace.sm + 1.0; // 9 px (closest token-derived value: sm=8 + 1)
  static const _kVPad = AppSpace.xxs; // 4 px
  static const _kGap = AppSpace.xxs + 1.0; // 5 px

  String get _label {
    return switch (kind) {
      Hangout.coffee => 'Coffee',
      Hangout.walk => 'Walk',
      Hangout.food => 'Food',
      Hangout.explore => 'Explore',
      Hangout.cowork => 'Cowork',
    };
  }

  Widget _icon() {
    return switch (kind) {
      Hangout.coffee => const IconCoffee(size: 12),
      Hangout.walk => const IconWalk(size: 12),
      Hangout.food => const IconFood(size: 12),
      Hangout.explore => const IconExplore(size: 12),
      Hangout.cowork => const IconCowork(size: 12),
    };
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: _kHPad, vertical: _kVPad),
      decoration: const BoxDecoration(
        color: AppColors.emberTint,
        borderRadius: AppRadius.pillR,
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          _icon(),
          const SizedBox(width: _kGap),
          Text(
            _label,
            // AppText.pill is 11/600; spec says 11/500
            style: AppText.pill.copyWith(
              fontWeight: FontWeight.w500,
              color: AppColors.emberDeep,
            ),
          ),
        ],
      ),
    );
  }
}
