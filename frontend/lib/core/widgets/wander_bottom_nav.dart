import 'dart:ui';

import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/tokens/app_radius.dart';
import 'package:wandermeet_app/core/tokens/app_typography.dart';

/// Custom glassy bottom navigation bar.
///
/// Uses [BackdropFilter] for the frosted-glass effect instead of the
/// stock [BottomNavigationBar] — the design requires blur: 20 +
/// saturate: 1.8 on a semi-transparent surface.
///
/// Four fixed items: Today / Places / Invites / You.
/// Active item icon + label use [AppColors.ember].
/// Inactive items use [AppColors.ink3] / [AppColors.dInk3] in dark.
///
/// Optional [incomingUnreadCount] renders a small ember badge on the
/// Invites icon. The badge is always ember (NEVER red — non-negotiable §2).
class WanderBottomNav extends StatelessWidget {
  const WanderBottomNav({
    super.key,
    required this.currentIndex,
    required this.onChange,
    this.incomingUnreadCount,
  });

  final int currentIndex;
  final ValueChanged<int> onChange;
  final int? incomingUnreadCount;

  static const _items = [
    _NavItem(label: 'Today', icon: Icons.home_outlined, activeIcon: Icons.home),
    _NavItem(
      label: 'Places',
      icon: Icons.place_outlined,
      activeIcon: Icons.place,
    ),
    _NavItem(
      label: 'Invites',
      icon: Icons.mail_outlined,
      activeIcon: Icons.mail,
    ),
    _NavItem(
      label: 'You',
      icon: Icons.person_outlined,
      activeIcon: Icons.person,
    ),
  ];

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final inactiveColor = isDark ? AppColors.dInk3 : AppColors.ink3;

    return ClipRect(
      child: BackdropFilter(
        filter: ImageFilter.compose(
          outer: ImageFilter.blur(sigmaX: 20, sigmaY: 20),
          inner: ColorFilter.matrix([
            1.8,
            0,
            0,
            0,
            -40,
            0,
            1.8,
            0,
            0,
            -40,
            0,
            0,
            1.8,
            0,
            -40,
            0,
            0,
            0,
            1,
            0,
          ]),
        ),
        child: Container(
          color: AppColors.surface.withValues(alpha: 0.88),
          child: SafeArea(
            top: false,
            child: SizedBox(
              height: 56,
              child: Row(
                children: List.generate(_items.length, (index) {
                  final item = _items[index];
                  final isActive = index == currentIndex;
                  final itemColor = isActive ? AppColors.ember : inactiveColor;
                  final showBadge =
                      index == 2 &&
                      incomingUnreadCount != null &&
                      incomingUnreadCount! > 0;

                  return Expanded(
                    child: GestureDetector(
                      behavior: HitTestBehavior.opaque,
                      onTap: () => onChange(index),
                      child: SizedBox(
                        height: 44,
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            _IconWithBadge(
                              icon: isActive ? item.activeIcon : item.icon,
                              color: itemColor,
                              badgeCount: showBadge
                                  ? incomingUnreadCount!
                                  : null,
                            ),
                            const SizedBox(height: 2),
                            Text(
                              item.label,
                              style: AppText.pill.copyWith(color: itemColor),
                            ),
                          ],
                        ),
                      ),
                    ),
                  );
                }),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _NavItem {
  const _NavItem({
    required this.label,
    required this.icon,
    required this.activeIcon,
  });

  final String label;
  final IconData icon;
  final IconData activeIcon;
}

/// Icon with an optional ember badge (top-right).
/// Badge is 14×14, background [AppColors.ember], pill-shaped.
class _IconWithBadge extends StatelessWidget {
  const _IconWithBadge({
    required this.icon,
    required this.color,
    this.badgeCount,
  });

  final IconData icon;
  final Color color;
  final int? badgeCount;

  String get _badgeLabel {
    final count = badgeCount ?? 0;
    return count > 99 ? '99+' : count.toString();
  }

  @override
  Widget build(BuildContext context) {
    return Stack(
      clipBehavior: Clip.none,
      children: [
        Icon(icon, size: 22, color: color),
        if (badgeCount != null && badgeCount! > 0)
          Positioned(
            top: -4,
            right: -6,
            child: Container(
              constraints: const BoxConstraints(minWidth: 14, minHeight: 14),
              padding: const EdgeInsets.symmetric(horizontal: 2),
              decoration: BoxDecoration(
                color: AppColors.ember,
                borderRadius: AppRadius.pillR,
              ),
              child: Center(
                child: Text(
                  _badgeLabel,
                  style: AppText.pill.copyWith(
                    color: AppColors.white,
                    fontSize: 9,
                    height: 1.4,
                  ),
                ),
              ),
            ),
          ),
      ],
    );
  }
}
