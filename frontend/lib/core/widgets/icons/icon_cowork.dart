import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';

/// CustomPainter for the cowork/laptop icon — file-private.
/// 24-grid, 1.75 px stroke, 2 px corner radius, foreground AppColors.ember.
class _IconCoworkPainter extends CustomPainter {
  const _IconCoworkPainter();

  @override
  void paint(Canvas canvas, Size size) {
    final double scale = size.width / 24;
    final strokePaint = Paint()
      ..color = AppColors.ember
      ..strokeWidth = 1.75 * scale
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round
      ..style = PaintingStyle.stroke;

    // laptop screen body
    final screenRect = RRect.fromRectAndRadius(
      Rect.fromLTWH(4.5 * scale, 6.5 * scale, 15 * scale, 9.5 * scale),
      const Radius.circular(1.5),
    );
    canvas.drawRRect(screenRect, strokePaint);

    // laptop base line
    final basePath = Path();
    basePath.moveTo(3 * scale, 18.5 * scale);
    basePath.lineTo(21 * scale, 18.5 * scale);
    canvas.drawPath(basePath, strokePaint);

    // presence dot in screen
    canvas.drawCircle(
      Offset(12 * scale, 11.25 * scale),
      1.25 * scale,
      strokePaint,
    );
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}

/// Cowork/laptop icon widget.
class IconCowork extends StatelessWidget {
  const IconCowork({super.key, this.size = 24});

  final double size;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: size,
      height: size,
      child: CustomPaint(painter: const _IconCoworkPainter()),
    );
  }
}
