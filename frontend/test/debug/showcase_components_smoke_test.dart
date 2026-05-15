// test/debug/showcase_components_smoke_test.dart
//
// Smoke tests for the Components section of ShowcasePage.
// Pixel-fidelity goldens deferred (see CLAUDE.md Flutter trap log).

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/tokens/app_theme.dart';
import 'package:wandermeet_app/debug/showcase_page.dart';

void main() {
  group('ShowcasePage components smoke', () {
    testWidgets('ShowcasePage_ComponentsSection_LightMode_BuildsCleanly', (
      tester,
    ) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.light,
          home: const ShowcasePage(showOnly: ShowcaseSection.components),
        ),
      );
      expect(find.byType(ShowcasePage), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('ShowcasePage_ComponentsSection_DarkMode_BuildsCleanly', (
      tester,
    ) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.dark,
          home: const ShowcasePage(showOnly: ShowcaseSection.components),
        ),
      );
      expect(find.byType(ShowcasePage), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('ShowcasePage_AllSections_LightMode_BuildsCleanly', (
      tester,
    ) async {
      await tester.pumpWidget(
        MaterialApp(theme: AppTheme.light, home: const ShowcasePage()),
      );
      expect(find.byType(ShowcasePage), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  });
}
