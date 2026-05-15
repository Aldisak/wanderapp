import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/widgets/wander_back_button.dart';

/// Minimal AppBar used on profile, invite detail, and other secondary screens.
///
/// - Background: [AppColors.paper], elevation 0, no shadow.
/// - Left slot: [WanderBackButton] when [backLabel] is non-null.
/// - Right slot: optional [trailing] widget (e.g. a report icon).
/// - No big title — each screen renders its own Newsreader hero in the body.
///
/// Implements [PreferredSizeWidget] so it can be passed directly to
/// [Scaffold.appBar].
class WanderAppBar extends StatelessWidget implements PreferredSizeWidget {
  const WanderAppBar({super.key, this.backLabel, this.trailing});

  final String? backLabel;
  final Widget? trailing;

  @override
  Size get preferredSize => const Size.fromHeight(kToolbarHeight);

  @override
  Widget build(BuildContext context) {
    final label = backLabel;
    return AppBar(
      backgroundColor: AppColors.paper,
      elevation: 0,
      scrolledUnderElevation: 0,
      automaticallyImplyLeading: false,
      leadingWidth: label != null ? 120 : 0,
      leading: label != null
          ? Padding(
              padding: const EdgeInsets.only(left: 8),
              child: WanderBackButton(label: label),
            )
          : null,
      actions: trailing != null
          ? [Padding(padding: const EdgeInsets.only(right: 8), child: trailing)]
          : null,
    );
  }
}
