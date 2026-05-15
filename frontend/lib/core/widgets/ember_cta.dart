import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_typography.dart';

/// Primary call-to-action button.
/// Wraps [ElevatedButton] so the pressed/disabled appearance comes from
/// [Theme.of(context).elevatedButtonTheme] (configured by AppTheme for the
/// 54 h filled-ember style with [AppRadius.lg] corners and ember glow).
///
/// Pressed: [AnimatedScale] 1.0 → 0.98 over 100 ms.
/// The scale animation is **only** active when [onPressed] is non-null.
/// When [onPressed] is null, the ElevatedButton renders disabled via the theme.
///
/// Optional trailing arrow icon (16×16 [Icons.arrow_forward]) when
/// [trailingArrow] is true.
class EmberCTA extends StatefulWidget {
  const EmberCTA({
    super.key,
    required this.label,
    required this.onPressed,
    this.trailingArrow = false,
    this.semanticsLabel,
  });

  final String label;
  final VoidCallback? onPressed;
  final bool trailingArrow;
  final String? semanticsLabel;

  @override
  State<EmberCTA> createState() => _EmberCTAState();
}

class _EmberCTAState extends State<EmberCTA> {
  bool _pressed = false;

  @override
  Widget build(BuildContext context) {
    final isEnabled = widget.onPressed != null;
    final targetScale = (isEnabled && _pressed) ? 0.98 : 1.0;

    final buttonChild = Row(
      mainAxisAlignment: MainAxisAlignment.center,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(widget.label, style: AppText.title),
        if (widget.trailingArrow) ...[
          const SizedBox(width: 8),
          const Icon(Icons.arrow_forward, size: 16),
        ],
      ],
    );

    return Listener(
      onPointerDown: isEnabled ? (_) => setState(() => _pressed = true) : null,
      onPointerUp: isEnabled ? (_) => setState(() => _pressed = false) : null,
      onPointerCancel: isEnabled
          ? (_) => setState(() => _pressed = false)
          : null,
      child: AnimatedScale(
        scale: targetScale,
        duration: const Duration(milliseconds: 100),
        child: SizedBox(
          width: double.infinity,
          child: ElevatedButton(
            onPressed: widget.onPressed,
            child: buttonChild,
          ),
        ),
      ),
    );
  }
}
