import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/widgets/hangout_tag.dart';

void main() {
  group('HangoutTag', () {
    testWidgets('HangoutTag_renders_all_five_hangouts_with_distinct_icons', (
      tester,
    ) async {
      for (final hangout in Hangout.values) {
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: Center(child: HangoutTag(kind: hangout)),
            ),
          ),
        );
        await tester.pump();
        expect(find.byType(HangoutTag), findsOneWidget);
      }
    });

    testWidgets(
      'HangoutTag_label_matches_coffee_walk_food_explore_cowork_table',
      (tester) async {
        final expectedLabels = {
          Hangout.coffee: 'Coffee',
          Hangout.walk: 'Walk',
          Hangout.food: 'Food',
          Hangout.explore: 'Explore',
          Hangout.cowork: 'Cowork',
        };

        for (final entry in expectedLabels.entries) {
          await tester.pumpWidget(
            MaterialApp(
              home: Scaffold(
                body: Center(child: HangoutTag(kind: entry.key)),
              ),
            ),
          );
          await tester.pump();
          expect(
            find.text(entry.value),
            findsOneWidget,
            reason: 'Expected label "${entry.value}" for ${entry.key}',
          );
        }
      },
    );

    testWidgets('HangoutTag_RendersCoffeeChipInDarkMode', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: ThemeData(brightness: Brightness.dark),
          home: const Scaffold(
            body: Center(child: HangoutTag(kind: Hangout.coffee)),
          ),
        ),
      );
      await tester.pump();
      // Even in dark mode, emberTint background remains visible
      expect(find.text('Coffee'), findsOneWidget);
      final container = tester.widget<Container>(find.byType(Container).first);
      final decoration = container.decoration as BoxDecoration?;
      expect(decoration?.color, equals(AppColors.emberTint));
    });

    testWidgets('HangoutTag_const_constructor_compiles', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(body: HangoutTag(kind: Hangout.coffee)),
        ),
      );
      expect(find.byType(HangoutTag), findsOneWidget);
    });
  });
}
