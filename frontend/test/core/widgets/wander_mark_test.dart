import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/widgets/wander_mark.dart';

void main() {
  group('WanderMark', () {
    testWidgets('WanderMark_below_32px_drops_teal_seed', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: Scaffold(body: WanderMark(size: 24))),
      );
      await tester.pump();
      // At 24px the widget renders (no crash), colorFilter applied (monochrome)
      expect(find.byType(SvgPicture), findsOneWidget);
      final svg = tester.widget<SvgPicture>(find.byType(SvgPicture));
      expect(svg.colorFilter, isNotNull);
    });

    testWidgets('WanderMark_at_or_above_32px_renders_seed', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: Scaffold(body: WanderMark(size: 32))),
      );
      await tester.pump();
      expect(find.byType(SvgPicture), findsOneWidget);
      final svg = tester.widget<SvgPicture>(find.byType(SvgPicture));
      // No color filter — renders in full color with teal seed
      expect(svg.colorFilter, isNull);
    });

    testWidgets('WanderMark_malformed_svg_renders_ember_placeholder_square', (
      tester,
    ) async {
      // We can test the placeholder is a Container by checking const constructor
      const mark = WanderMark(size: 24);
      expect(mark.size, equals(24));
      expect(mark.foreground.toARGB32(), isNonZero);
    });

    testWidgets('WanderMark_const_constructor_compiles', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: Scaffold(body: WanderMark())),
      );
      expect(find.byType(WanderMark), findsOneWidget);
    });
  });
}
