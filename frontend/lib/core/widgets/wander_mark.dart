import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';

/// The WanderMeet brand mark.
/// Renders assets/mark.svg via flutter_svg.
/// Below 32 px: monochrome (no teal seed).
/// At ≥ 32 px: renders with teal seed.
/// On parse failure: 24×24 ember placeholder square.
class WanderMark extends StatelessWidget {
  const WanderMark({
    super.key,
    this.size = 24,
    this.foreground = AppColors.emberDeep,
    this.seed = AppColors.teal,
  });

  final double size;
  final Color foreground;
  final Color seed;

  @override
  Widget build(BuildContext context) {
    final showSeed = size >= 32;
    return SvgPicture.asset(
      'assets/mark.svg',
      width: size,
      height: size,
      colorFilter: showSeed
          ? null
          : ColorFilter.mode(foreground, BlendMode.srcIn),
      placeholderBuilder: (_) =>
          Container(width: size, height: size, color: AppColors.ember),
    );
  }
}
