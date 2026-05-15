import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/widgets/ember_cta.dart';

void main() {
  group('EmberCTA', () {
    testWidgets('EmberCTA_renders_arrow_when_trailingArrow_true', (
      tester,
    ) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: EmberCTA(
              label: "Let's meet",
              onPressed: () {},
              trailingArrow: true,
            ),
          ),
        ),
      );
      await tester.pump();
      expect(find.byIcon(Icons.arrow_forward), findsOneWidget);
    });

    testWidgets('EmberCTA_no_arrow_when_trailingArrow_false', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: EmberCTA(label: 'Send invite', onPressed: () {}),
          ),
        ),
      );
      await tester.pump();
      expect(find.byIcon(Icons.arrow_forward), findsNothing);
    });

    testWidgets('EmberCTA_has_semantic_label_from_visible_text', (
      tester,
    ) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: EmberCTA(label: "Let's meet", onPressed: () {}),
          ),
        ),
      );
      await tester.pump();
      expect(find.text("Let's meet"), findsOneWidget);
    });

    testWidgets('EmberCTA_has_semantic_label_from_semanticsLabel_override', (
      tester,
    ) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: EmberCTA(
              label: "Let's meet",
              onPressed: () {},
              semanticsLabel: 'Confirm meeting request',
            ),
          ),
        ),
      );
      await tester.pump();
      expect(find.text("Let's meet"), findsOneWidget);
    });

    testWidgets('EmberCTA_wraps_ElevatedButton', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: EmberCTA(label: 'Tap me', onPressed: () {}),
          ),
        ),
      );
      await tester.pump();
      expect(find.byType(ElevatedButton), findsOneWidget);
    });

    testWidgets('EmberCTA_onPressed_null_disables_ElevatedButton', (
      tester,
    ) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(body: EmberCTA(label: 'Disabled', onPressed: null)),
        ),
      );
      await tester.pump();
      final btn = tester.widget<ElevatedButton>(find.byType(ElevatedButton));
      expect(btn.onPressed, isNull);
    });

    testWidgets('EmberCTA_AnimatedScale_is_1_0_when_not_pressed', (
      tester,
    ) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: EmberCTA(label: 'Tap me', onPressed: () {}),
          ),
        ),
      );
      await tester.pump();
      final animatedScale = tester.widget<AnimatedScale>(
        find.byType(AnimatedScale),
      );
      expect(animatedScale.scale, equals(1.0));
    });

    testWidgets(
      'EmberCTA_AnimatedScale_targets_0_98_and_duration_100ms_when_pressed',
      (tester) async {
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: EmberCTA(label: 'Tap me', onPressed: () {}),
            ),
          ),
        );
        await tester.pump();

        // Start a press gesture (tap down without tap up).
        final gesture = await tester.startGesture(
          tester.getCenter(find.byType(EmberCTA)),
        );
        await tester.pump();

        final animatedScale = tester.widget<AnimatedScale>(
          find.byType(AnimatedScale),
        );
        // Scale should now be targeting 0.98 (state is _pressed = true).
        expect(animatedScale.scale, equals(0.98));
        // Duration should be 100 ms.
        expect(
          animatedScale.duration,
          equals(const Duration(milliseconds: 100)),
        );

        await gesture.up();
        await tester.pumpAndSettle();
      },
    );

    testWidgets('EmberCTA_AnimatedScale_stays_1_0_when_disabled', (
      tester,
    ) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(body: EmberCTA(label: 'Disabled', onPressed: null)),
        ),
      );
      await tester.pump();

      // Simulate a tap-down on a disabled button.
      final gesture = await tester.startGesture(
        tester.getCenter(find.byType(EmberCTA)),
      );
      await tester.pump();

      final animatedScale = tester.widget<AnimatedScale>(
        find.byType(AnimatedScale),
      );
      // Scale must stay at 1.0 when disabled.
      expect(animatedScale.scale, equals(1.0));

      await gesture.up();
      await tester.pumpAndSettle();
    });

    testWidgets('EmberCTA_min_tap_target_height_54', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: EmberCTA(label: 'Tap', onPressed: () {}),
          ),
        ),
      );
      await tester.pump();
      final size = tester.getSize(find.byType(EmberCTA));
      expect(size.height, greaterThanOrEqualTo(44));
    });
  });
}
