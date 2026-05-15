import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/widgets/wander_check_pill.dart';

void main() {
  group('WanderCheckPill', () {
    testWidgets(
      'WanderCheckPill_active_state_uses_emberTint_bg_ember_border_emberDeep_text',
      (tester) async {
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: Center(
                child: WanderCheckPill(
                  label: 'Great vibe',
                  active: true,
                  onTap: () {},
                ),
              ),
            ),
          ),
        );
        await tester.pump();

        final container = tester.widget<Container>(
          find.byType(Container).first,
        );
        final decoration = container.decoration as BoxDecoration?;
        expect(decoration?.color, equals(AppColors.emberTint));
        expect(
          (decoration?.border as Border?)?.top.color,
          equals(AppColors.ember),
        );

        final textWidget = tester.widget<Text>(find.text('Great vibe'));
        expect(textWidget.style?.color, equals(AppColors.emberDeep));
      },
    );

    testWidgets('WanderCheckPill_inactive_state_uses_surface_bg_line_border', (
      tester,
    ) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: Center(
              child: WanderCheckPill(
                label: 'No chemistry',
                active: false,
                onTap: () {},
              ),
            ),
          ),
        ),
      );
      await tester.pump();

      final container = tester.widget<Container>(find.byType(Container).first);
      final decoration = container.decoration as BoxDecoration?;
      expect(decoration?.color, equals(AppColors.surface));
      expect(
        (decoration?.border as Border?)?.top.color,
        equals(AppColors.line),
      );
    });

    testWidgets('WanderCheckPill_tap_invokes_onTap', (tester) async {
      bool tapped = false;
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: Center(
              child: WanderCheckPill(
                label: 'Great vibe',
                active: false,
                onTap: () => tapped = true,
              ),
            ),
          ),
        ),
      );
      await tester.tap(find.byType(WanderCheckPill));
      await tester.pump();
      expect(tapped, isTrue);
    });

    testWidgets('WanderCheckPill_min_tap_target_44px', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: Center(
              child: WanderCheckPill(label: 'Tag', active: false, onTap: () {}),
            ),
          ),
        ),
      );
      await tester.pump();
      final size = tester.getSize(find.byType(WanderCheckPill));
      expect(size.height, greaterThanOrEqualTo(44));
    });

    testWidgets('WanderCheckPill_has_semantics_label', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: Center(
              child: WanderCheckPill(
                label: 'Great vibe',
                active: true,
                onTap: () {},
              ),
            ),
          ),
        ),
      );
      await tester.pump();
      // Verify Semantics widget is present
      expect(find.byType(Semantics), findsAtLeastNWidgets(1));
      // Visible label text is rendered
      expect(find.text('Great vibe'), findsOneWidget);
    });

    testWidgets(
      'WanderCheckPill_active_trailing_indicator_shows_check_icon_on_ember_fill',
      (tester) async {
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: Center(
                child: WanderCheckPill(
                  label: 'Great vibe',
                  active: true,
                  onTap: () {},
                ),
              ),
            ),
          ),
        );
        await tester.pump();
        // Active: filled ember circle containing a check icon.
        expect(find.byIcon(Icons.check), findsOneWidget);
      },
    );

    testWidgets(
      'WanderCheckPill_inactive_trailing_indicator_shows_no_check_icon',
      (tester) async {
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: Center(
                child: WanderCheckPill(
                  label: 'No chemistry',
                  active: false,
                  onTap: () {},
                ),
              ),
            ),
          ),
        );
        await tester.pump();
        // Inactive: hollow ring — no check icon present.
        expect(find.byIcon(Icons.check), findsNothing);
      },
    );
  });
}
