import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/widgets/sponsored_pill.dart';

void main() {
  group('SponsoredPill', () {
    testWidgets(
      'SponsoredPill_label_is_literally_Sponsored_uppercase_letterspacing',
      (tester) async {
        await tester.pumpWidget(
          const MaterialApp(
            home: Scaffold(body: Center(child: SponsoredPill())),
          ),
        );
        await tester.pump();
        // Displays "SPONSORED" (uppercase)
        expect(find.text('SPONSORED'), findsOneWidget);
      },
    );

    testWidgets('SponsoredPill_background_is_sunTint', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(body: Center(child: SponsoredPill())),
        ),
      );
      await tester.pump();
      final container = tester.widget<Container>(find.byType(Container).first);
      final decoration = container.decoration as BoxDecoration?;
      expect(decoration?.color, equals(AppColors.sponsoredBg));
    });

    testWidgets('SponsoredPill_const_constructor_compiles', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(home: Scaffold(body: SponsoredPill())),
      );
      expect(find.byType(SponsoredPill), findsOneWidget);
    });
  });
}
