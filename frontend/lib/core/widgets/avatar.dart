import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/tokens/app_typography.dart';
import 'package:wandermeet_app/core/tokens/app_radius.dart';

/// Avatar hue determines the background color of the placeholder.
enum AvatarHue { ember, iris, teal, clay, sand, stone, emberDeep }

/// Faceless silhouette avatar.
/// When [imageUrl] is provided, renders a circular network image.
/// Otherwise renders the [initial] letter on a colored background.
class Avatar extends StatelessWidget {
  const Avatar({
    super.key,
    required this.initial,
    this.size = 56,
    this.hue = AvatarHue.ember,
    this.imageUrl,
  });

  final String initial;
  final double size;
  final AvatarHue hue;
  final String? imageUrl;

  Color get _background {
    return switch (hue) {
      AvatarHue.ember => AppColors.ember,
      AvatarHue.emberDeep => AppColors.emberDeep,
      AvatarHue.iris => AppColors.iris,
      AvatarHue.teal => AppColors.teal,
      AvatarHue.clay => AppColors.clay,
      AvatarHue.sand => AppColors.sand,
      AvatarHue.stone => AppColors.stone,
    };
  }

  Color get _textColor {
    return switch (hue) {
      AvatarHue.sand => AppColors.ink,
      AvatarHue.stone => AppColors.ink,
      _ => AppColors.white,
    };
  }

  @override
  Widget build(BuildContext context) {
    final url = imageUrl;
    if (url != null && url.isNotEmpty) {
      return ClipOval(
        child: CachedNetworkImage(
          imageUrl: url,
          width: size,
          height: size,
          fit: BoxFit.cover,
        ),
      );
    }

    final fontSize = size * 0.42;
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: _background,
        borderRadius: BorderRadius.circular(AppRadius.pill),
      ),
      clipBehavior: Clip.hardEdge,
      child: Stack(
        children: [
          // highlight circle offset 20% top-right
          Positioned(
            right: -size * 0.1,
            top: -size * 0.1,
            child: Container(
              width: size * 0.6,
              height: size * 0.6,
              decoration: BoxDecoration(
                color: AppColors.white.withValues(alpha: 0.15),
                shape: BoxShape.circle,
              ),
            ),
          ),
          Center(
            child: Text(
              initial.isEmpty ? '' : initial[0].toUpperCase(),
              style: AppText.serif(
                size: fontSize,
                weight: FontWeight.w400,
                color: _textColor,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
