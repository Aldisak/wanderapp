import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/tokens/app_radius.dart';
import 'package:wandermeet_app/core/tokens/app_typography.dart';

/// Big chip button used on the Review screen.
/// Active state: emberTint background, ember border, emberDeep text.
/// Inactive state: surface background, line border, ink2 text.
///
/// Trailing indicator (22×22):
///   • Inactive: hollow circle with 1.5 px [AppColors.ink4] ring.
///   • Active: filled [AppColors.ember] circle with a centred white check icon.
class WanderCheckPill extends StatelessWidget {
  const WanderCheckPill({
    super.key,
    required this.label,
    required this.active,
    required this.onTap,
    this.semanticsLabel,
  });

  final String label;
  final bool active;
  final VoidCallback? onTap;
  final String? semanticsLabel;

  @override
  Widget build(BuildContext context) {
    final bgColor = active ? AppColors.emberTint : AppColors.surface;
    final borderColor = active ? AppColors.ember : AppColors.line;
    final textColor = active ? AppColors.emberDeep : AppColors.ink2;

    return Semantics(
      label: semanticsLabel ?? label,
      button: true,
      selected: active,
      child: GestureDetector(
        onTap: onTap,
        child: Container(
          constraints: const BoxConstraints(minHeight: 44, minWidth: 44),
          padding: const EdgeInsets.symmetric(
            horizontal: AppSpace.lg,
            vertical: AppSpace.sm,
          ),
          decoration: BoxDecoration(
            color: bgColor,
            borderRadius: AppRadius.pillR,
            border: Border.all(color: borderColor),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(label, style: AppText.pill.copyWith(color: textColor)),
              const SizedBox(width: AppSpace.sm),
              _TrailingIndicator(active: active),
            ],
          ),
        ),
      ),
    );
  }
}

/// 22×22 trailing circle indicator for [WanderCheckPill].
class _TrailingIndicator extends StatelessWidget {
  const _TrailingIndicator({required this.active});

  final bool active;

  @override
  Widget build(BuildContext context) {
    if (active) {
      return Container(
        width: 22,
        height: 22,
        decoration: const BoxDecoration(
          color: AppColors.ember,
          shape: BoxShape.circle,
        ),
        child: const Icon(Icons.check, color: AppColors.white, size: 14),
      );
    }

    return Container(
      width: 22,
      height: 22,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        border: Border.all(color: AppColors.ink4, width: 1.5),
      ),
    );
  }
}
