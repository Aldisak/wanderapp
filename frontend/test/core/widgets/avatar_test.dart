import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/widgets/avatar.dart';

void main() {
  group('Avatar', () {
    testWidgets('Avatar_renders_initial_when_imageUrl_null', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: Center(child: Avatar(initial: 'S')),
          ),
        ),
      );
      await tester.pump();
      expect(find.text('S'), findsOneWidget);
    });

    testWidgets('Avatar_all_seven_hues_resolve_distinct_backgrounds', (
      tester,
    ) async {
      final hues = AvatarHue.values;
      final backgrounds = <Color>{};
      for (final hue in hues) {
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: Center(
                child: Avatar(initial: 'T', hue: hue),
              ),
            ),
          ),
        );
        await tester.pump();
        final container = tester.widget<Container>(
          find.byType(Container).first,
        );
        final decoration = container.decoration as BoxDecoration;
        backgrounds.add(decoration.color!);
      }
      // All seven hues should have distinct colors
      expect(backgrounds.length, equals(7));
    });

    testWidgets('Avatar_sand_and_stone_use_ink_text', (tester) async {
      for (final hue in [AvatarHue.sand, AvatarHue.stone]) {
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: Center(
                child: Avatar(initial: 'T', hue: hue),
              ),
            ),
          ),
        );
        await tester.pump();
        final textWidget = tester.widget<Text>(find.text('T'));
        expect(textWidget.style?.color, equals(AppColors.ink));
      }
    });

    testWidgets('Avatar_RendersInitialContrastInDarkMode', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: ThemeData(brightness: Brightness.dark),
          home: const Scaffold(
            body: Center(
              child: Avatar(initial: 'D', hue: AvatarHue.ember),
            ),
          ),
        ),
      );
      await tester.pump();
      // Ember hue uses white text even in dark mode
      final textWidget = tester.widget<Text>(find.text('D'));
      expect(textWidget.style?.color, equals(AppColors.white));
    });

    testWidgets('Avatar_const_constructor_compiles', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(body: Avatar(initial: 'A')),
        ),
      );
      expect(find.byType(Avatar), findsOneWidget);
    });
  });
}
