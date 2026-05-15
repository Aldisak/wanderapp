// test/debug/showcase_shadows_smoke_test.dart
//
// Smoke tests for the Shadows section of ShowcasePage.

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/tokens/app_theme.dart';
import 'package:wandermeet_app/debug/showcase_page.dart';

void main() {
  group('ShowcasePage shadows smoke', () {
    testWidgets('ShowcasePage_ShadowsSection_LightMode_BuildsCleanly', (
      tester,
    ) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.light,
          home: const ShowcasePage(showOnly: ShowcaseSection.shadows),
        ),
      );
      expect(find.byType(ShowcasePage), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('ShowcasePage_ShadowsSection_DarkMode_BuildsCleanly', (
      tester,
    ) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.dark,
          home: const ShowcasePage(showOnly: ShowcaseSection.shadows),
        ),
      );
      expect(find.byType(ShowcasePage), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  });
}
