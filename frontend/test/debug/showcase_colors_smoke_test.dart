// test/debug/showcase_colors_smoke_test.dart
//
// Smoke tests for the Colours section of ShowcasePage.
// Pixel-fidelity goldens are deferred: see CLAUDE.md "Flutter trap log"
// for the google_fonts asset-bundling carve-out. The reference PNGs at
// test/debug/goldens/ are kept as artwork references; they are NOT
// re-validated until the bundle ships.

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/tokens/app_theme.dart';
import 'package:wandermeet_app/debug/showcase_page.dart';

void main() {
  group('ShowcasePage colours smoke', () {
    testWidgets('ShowcasePage_ColorsSection_LightMode_BuildsCleanly', (
      tester,
    ) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.light,
          home: const ShowcasePage(showOnly: ShowcaseSection.colours),
        ),
      );
      expect(find.byType(ShowcasePage), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('ShowcasePage_ColorsSection_DarkMode_BuildsCleanly', (
      tester,
    ) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.dark,
          home: const ShowcasePage(showOnly: ShowcaseSection.colours),
        ),
      );
      expect(find.byType(ShowcasePage), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  });
}
