// test/debug/showcase_typography_smoke_test.dart
//
// Smoke tests for the Typography section of ShowcasePage.
// Pixel-fidelity goldens deferred (see CLAUDE.md Flutter trap log).

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/tokens/app_theme.dart';
import 'package:wandermeet_app/debug/showcase_page.dart';

void main() {
  group('ShowcasePage typography smoke', () {
    testWidgets('ShowcasePage_TypographySection_LightMode_BuildsCleanly', (
      tester,
    ) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.light,
          home: const ShowcasePage(showOnly: ShowcaseSection.typography),
        ),
      );
      expect(find.byType(ShowcasePage), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('ShowcasePage_TypographySection_DarkMode_BuildsCleanly', (
      tester,
    ) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.dark,
          home: const ShowcasePage(showOnly: ShowcaseSection.typography),
        ),
      );
      expect(find.byType(ShowcasePage), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  });
}
