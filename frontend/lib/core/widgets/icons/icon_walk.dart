import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';

/// CustomPainter for the walk/hike icon (file-private).
/// 24-grid, 1.75 px stroke, 2 px corner radius, foreground AppColors.ember.
class _IconWalkPainter extends CustomPainter {
  const _IconWalkPainter();

  @override
  void paint(Canvas canvas, Size size) {
    final double scale = size.width / 24;
    final paint = Paint()
      ..color = AppColors.ember
      ..strokeWidth = 1.75 * scale
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round
      ..style = PaintingStyle.stroke;

    // head circle
    canvas.drawCircle(Offset(14 * scale, 4.75 * scale), 1.75 * scale, paint);

    // torso + arm + leg
    final torsoPath = Path();
    torsoPath.moveTo(14 * scale, 7.5 * scale);
    torsoPath.lineTo(11.5 * scale, 13 * scale);
    torsoPath.lineTo(14.5 * scale, 14.5 * scale);
    torsoPath.lineTo(13 * scale, 19.5 * scale);
    canvas.drawPath(torsoPath, paint);

    // right leg
    final legPath = Path();
    legPath.moveTo(14.5 * scale, 14.5 * scale);
    legPath.lineTo(17 * scale, 19 * scale);
    canvas.drawPath(legPath, paint);

    // left arm
    final armPath = Path();
    armPath.moveTo(11.5 * scale, 13 * scale);
    armPath.lineTo(8 * scale, 14.5 * scale);
    canvas.drawPath(armPath, paint);

    // upper arm
    final upperArmPath = Path();
    upperArmPath.moveTo(11.5 * scale, 9.5 * scale);
    upperArmPath.lineTo(9.5 * scale, 10 * scale);
    canvas.drawPath(upperArmPath, paint);
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}

/// Walk/hike icon widget.
class IconWalk extends StatelessWidget {
  const IconWalk({super.key, this.size = 24});

  final double size;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: size,
      height: size,
      child: CustomPaint(painter: const _IconWalkPainter()),
    );
  }
}
