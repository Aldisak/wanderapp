import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/widgets/wander_text_field.dart';

void main() {
  group('WanderTextField', () {
    testWidgets('WanderTextField_renders_with_hint', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(body: WanderTextField(hint: 'Enter text')),
        ),
      );
      await tester.pump();
      expect(find.byType(TextField), findsOneWidget);
    });

    testWidgets('WanderTextField_focus_border_is_15px_ember', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(body: WanderTextField(hint: 'Type here')),
        ),
      );
      await tester.pump();

      // Tap to focus
      await tester.tap(find.byType(TextField));
      await tester.pump();

      // Verify focused border is ember
      final TextField textField = tester.widget<TextField>(
        find.byType(TextField),
      );
      final InputDecoration? decoration = textField.decoration;
      final focusedBorder = decoration?.focusedBorder as OutlineInputBorder?;
      expect(focusedBorder?.borderSide.color, equals(AppColors.ember));
      expect(focusedBorder?.borderSide.width, closeTo(1.5, 0.01));
    });

    testWidgets('WanderTextField_renders_label', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: WanderTextField(label: 'Name', hint: 'Your name'),
          ),
        ),
      );
      await tester.pump();
      expect(find.text('Name'), findsOneWidget);
    });

    testWidgets('WanderTextField_const_constructor_compiles', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: Scaffold(body: WanderTextField())),
      );
      expect(find.byType(WanderTextField), findsOneWidget);
    });
  });
}
