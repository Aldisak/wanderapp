import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';

/// CustomPainter for the explore/compass icon — file-private.
/// 24-grid, 1.75 px stroke, 2 px corner radius, foreground AppColors.ember.
class _IconExplorePainter extends CustomPainter {
  const _IconExplorePainter();

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

    // compass circle
    canvas.drawCircle(Offset(12 * scale, 12 * scale), 8 * scale, strokePaint);

    // diamond needle
    final needlePath = Path();
    needlePath.moveTo(14.8 * scale, 9.2 * scale);
    needlePath.lineTo(13.4 * scale, 13.4 * scale);
    needlePath.lineTo(9.2 * scale, 14.8 * scale);
    needlePath.lineTo(10.6 * scale, 10.6 * scale);
    needlePath.close();
    canvas.drawPath(needlePath, strokePaint);

    // center dot (filled)
    canvas.drawCircle(Offset(12 * scale, 12 * scale), 0.5 * scale, fillPaint);
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}

/// Explore/compass icon widget.
class IconExplore extends StatelessWidget {
  const IconExplore({super.key, this.size = 24});

  final double size;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: size,
      height: size,
      child: CustomPaint(painter: const _IconExplorePainter()),
    );
  }
}
