import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/tokens/app_typography.dart';

/// Back navigation affordance used inside [WanderAppBar].
///
/// Renders a chevron-left icon followed by a Newsreader italic label
/// (e.g. "← Today"). Both are ember-colored.
/// Tap target is ≥ 44×44 (enforced via [ConstrainedBox]).
///
/// On tap: calls [onTap] if provided, otherwise calls
/// [Navigator.maybePop(context)].
class WanderBackButton extends StatelessWidget {
  const WanderBackButton({super.key, required this.label, this.onTap});

  final String label;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap ?? () => Navigator.maybePop(context),
      child: ConstrainedBox(
        constraints: const BoxConstraints(minWidth: 44, minHeight: 44),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.chevron_left, color: AppColors.ember, size: 22),
            Text(label, style: AppText.displayItalic(size: 16)),
          ],
        ),
      ),
    );
  }
}
