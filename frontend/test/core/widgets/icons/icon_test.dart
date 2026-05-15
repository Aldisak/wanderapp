import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/widgets/icons/icon_coffee.dart';
import 'package:wandermeet_app/core/widgets/icons/icon_cowork.dart';
import 'package:wandermeet_app/core/widgets/icons/icon_explore.dart';
import 'package:wandermeet_app/core/widgets/icons/icon_food.dart';
import 'package:wandermeet_app/core/widgets/icons/icon_walk.dart';

void main() {
  group('Hangout icons', () {
    testWidgets('IconCoffee_renders_at_24px', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: Scaffold(body: IconCoffee())),
      );
      await tester.pump();
      expect(find.byType(IconCoffee), findsOneWidget);
      final size = tester.getSize(find.byType(SizedBox).last);
      expect(size.width, equals(24));
      expect(size.height, equals(24));
    });

    testWidgets('IconWalk_renders_at_24px', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: Scaffold(body: IconWalk())),
      );
      await tester.pump();
      expect(find.byType(IconWalk), findsOneWidget);
    });

    testWidgets('IconFood_renders_at_24px', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: Scaffold(body: IconFood())),
      );
      await tester.pump();
      expect(find.byType(IconFood), findsOneWidget);
    });

    testWidgets('IconExplore_renders_at_24px', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: Scaffold(body: IconExplore())),
      );
      await tester.pump();
      expect(find.byType(IconExplore), findsOneWidget);
    });

    testWidgets('IconCowork_renders_at_24px', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: Scaffold(body: IconCowork())),
      );
      await tester.pump();
      expect(find.byType(IconCowork), findsOneWidget);
    });

    testWidgets('Each icon widget uses CustomPaint internally', (tester) async {
      for (final icon in [
        const IconCoffee(),
        const IconWalk(),
        const IconFood(),
        const IconExplore(),
        const IconCowork(),
      ]) {
        await tester.pumpWidget(MaterialApp(home: Scaffold(body: icon)));
        await tester.pump();
        expect(
          find.descendant(
            of: find.byWidget(icon),
            matching: find.byType(CustomPaint),
          ),
          findsOneWidget,
          reason: '${icon.runtimeType} must contain exactly one CustomPaint',
        );
      }
    });
  });
}
