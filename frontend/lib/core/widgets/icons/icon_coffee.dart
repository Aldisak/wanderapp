import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';

/// CustomPainter for the coffee icon (file-private).
/// 24-grid, 1.75 px stroke, 2 px corner radius, foreground AppColors.ember.
class _IconCoffeePainter extends CustomPainter {
  const _IconCoffeePainter();

  @override
  void paint(Canvas canvas, Size size) {
    final double scale = size.width / 24;
    final paint = Paint()
      ..color = AppColors.ember
      ..strokeWidth = 1.75 * scale
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round
      ..style = PaintingStyle.stroke;

    // mug body
    final mugPath = Path();
    mugPath.moveTo(5 * scale, 8 * scale);
    mugPath.lineTo(16 * scale, 8 * scale);
    mugPath.lineTo(16 * scale, 14.5 * scale);
    mugPath.arcToPoint(
      Offset(9 * scale, 18.5 * scale),
      radius: Radius.circular(4 * scale),
    );
    mugPath.lineTo(9 * scale, 18.5 * scale);
    mugPath.arcToPoint(
      Offset(5 * scale, 14.5 * scale),
      radius: Radius.circular(4 * scale),
    );
    mugPath.close();
    canvas.drawPath(mugPath, paint);

    // handle
    final handlePath = Path();
    handlePath.moveTo(16 * scale, 10 * scale);
    handlePath.lineTo(18.25 * scale, 10 * scale);
    handlePath.arcToPoint(
      Offset(18.25 * scale, 14 * scale),
      radius: Radius.circular(2 * scale),
    );
    handlePath.lineTo(16 * scale, 14 * scale);
    canvas.drawPath(handlePath, paint);

    // steam 1
    final steam1 = Path();
    steam1.moveTo(9 * scale, 4.5 * scale);
    steam1.cubicTo(
      8.4 * scale,
      5.3 * scale,
      8.4 * scale,
      6.2 * scale,
      9 * scale,
      7 * scale,
    );
    canvas.drawPath(steam1, paint);

    // steam 2
    final steam2 = Path();
    steam2.moveTo(12 * scale, 4 * scale);
    steam2.cubicTo(
      11.4 * scale,
      4.8 * scale,
      11.4 * scale,
      5.7 * scale,
      12 * scale,
      6.5 * scale,
    );
    canvas.drawPath(steam2, paint);
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}

/// Coffee-mug icon widget.
class IconCoffee extends StatelessWidget {
  const IconCoffee({super.key, this.size = 24});

  final double size;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: size,
      height: size,
      child: CustomPaint(painter: const _IconCoffeePainter()),
    );
  }
}
