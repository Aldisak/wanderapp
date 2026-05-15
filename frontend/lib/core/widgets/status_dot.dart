import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/tokens/app_radius.dart';

/// The three states of user activity.
/// Color AND shape carry meaning (color-blind safe).
enum ActivityStatus { online, recent, hidden }

/// 3-state activity indicator dot.
/// - [online]: filled green circle with halo
/// - [recent]: hollow yellow ring, transparent fill
/// - [hidden]: renders nothing (SizedBox.shrink)
class StatusDot extends StatelessWidget {
  const StatusDot({super.key, required this.status, this.size = 10});

  final ActivityStatus status;
  final double size;

  @override
  Widget build(BuildContext context) {
    switch (status) {
      case ActivityStatus.hidden:
        return const SizedBox.shrink();
      case ActivityStatus.online:
        return Container(
          width: size,
          height: size,
          decoration: BoxDecoration(
            color: AppColors.statusGreen,
            borderRadius: BorderRadius.circular(AppRadius.pill),
            boxShadow: [
              BoxShadow(
                color: AppColors.statusGreen.withValues(alpha: 0.18),
                blurRadius: size * 0.3,
                spreadRadius: size * 0.3,
              ),
            ],
          ),
        );
      case ActivityStatus.recent:
        return Container(
          width: size,
          height: size,
          decoration: BoxDecoration(
            color: Colors.transparent,
            borderRadius: BorderRadius.circular(AppRadius.pill),
            border: Border.all(color: AppColors.statusYellow, width: 2),
          ),
        );
    }
  }
}
