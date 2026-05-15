import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/tokens/app_radius.dart';

/// Cupertino-style custom toggle.
/// Track: 38×22, thumb: 18×18.
/// On state: teal track. Off state: line track.
/// Used for is_open_today and i_am_here_now only.
class WanderToggle extends StatelessWidget {
  const WanderToggle({
    super.key,
    required this.value,
    required this.onChanged,
    this.semanticsLabel,
  });

  final bool value;
  final ValueChanged<bool>? onChanged;
  final String? semanticsLabel;

  static const _trackWidth = 38.0;
  static const _trackHeight = 22.0;
  static const _thumbSize = 18.0;
  static const _thumbPad = (_trackHeight - _thumbSize) / 2; // 2 px

  @override
  Widget build(BuildContext context) {
    final trackColor = value ? AppColors.teal : AppColors.line;

    return Semantics(
      label: semanticsLabel ?? (value ? 'On' : 'Off'),
      toggled: value,
      child: GestureDetector(
        onTap: onChanged != null ? () => onChanged!(!value) : null,
        child: SizedBox(
          width: _trackWidth,
          height: _trackHeight,
          child: Stack(
            children: [
              // Track
              Positioned.fill(
                child: Container(
                  decoration: BoxDecoration(
                    color: trackColor,
                    borderRadius: AppRadius.pillR,
                  ),
                ),
              ),
              // Thumb
              AnimatedPositioned(
                duration: const Duration(milliseconds: 150),
                curve: Curves.easeInOut,
                top: _thumbPad,
                left: value ? _trackWidth - _thumbSize - _thumbPad : _thumbPad,
                child: Container(
                  width: _thumbSize,
                  height: _thumbSize,
                  decoration: const BoxDecoration(
                    color: AppColors.white,
                    shape: BoxShape.circle,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
