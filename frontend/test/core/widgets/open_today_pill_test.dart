import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/widgets/open_today_pill.dart';

void main() {
  group('OpenTodayPill', () {
    testWidgets('OpenTodayPill_label_is_literally_Open_today', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(body: Center(child: OpenTodayPill())),
        ),
      );
      await tester.pump();
      expect(find.text('Open today'), findsOneWidget);
    });

    testWidgets('OpenTodayPill_background_is_teal', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(body: Center(child: OpenTodayPill())),
        ),
      );
      await tester.pump();
      final containers = tester.widgetList<Container>(find.byType(Container));
      final outer = containers.first;
      final decoration = outer.decoration as BoxDecoration?;
      expect(decoration?.color, equals(AppColors.teal));
    });

    testWidgets('OpenTodayPill_const_constructor_compiles', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: Scaffold(body: OpenTodayPill())),
      );
      expect(find.byType(OpenTodayPill), findsOneWidget);
    });
  });
}
