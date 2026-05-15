import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';

/// CustomPainter for the food icon (plate + cloche) — file-private.
/// 24-grid, 1.75 px stroke, 2 px corner radius, foreground AppColors.ember.
class _IconFoodPainter extends CustomPainter {
  const _IconFoodPainter();

  @override
  void paint(Canvas canvas, Size size) {
    final double scale = size.width / 24;
    final strokePaint = Paint()
      ..color = AppColors.ember
      ..strokeWidth = 1.75 * scale
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round
      ..style = PaintingStyle.stroke;

    final fillPaint = Paint()
      ..color = AppColors.ember
      ..style = PaintingStyle.fill;

    // plate ellipse
    canvas.drawOval(
      Rect.fromCenter(
        center: Offset(12 * scale, 14.5 * scale),
        width: 16 * scale,
        height: 3 * scale,
      ),
      strokePaint,
    );

    // dome / cloche
    final domePath = Path();
    domePath.moveTo(5 * scale, 13.5 * scale);
    domePath.arcToPoint(
      Offset(19 * scale, 13.5 * scale),
      radius: Radius.circular(7 * scale),
      clockwise: false,
    );
    canvas.drawPath(domePath, strokePaint);

    // stem
    final stemPath = Path();
    stemPath.moveTo(12 * scale, 6.5 * scale);
    stemPath.lineTo(12 * scale, 5.5 * scale);
    canvas.drawPath(stemPath, strokePaint);

    // top dot (filled)
    canvas.drawCircle(Offset(12 * scale, 5 * scale), 0.6 * scale, fillPaint);
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}

/// Food icon widget.
class IconFood extends StatelessWidget {
  const IconFood({super.key, this.size = 24});

  final double size;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: size,
      height: size,
      child: CustomPaint(painter: const _IconFoodPainter()),
    );
  }
}
