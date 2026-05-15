// test/core/tokens/token_files_test.dart
//
// WI-1: verify token files import cleanly and expose expected symbols.
//
// google_fonts requires the Flutter binding to be initialized before
// accessing font-dependent TextStyle getters. Tests that touch AppText or
// AppTheme must therefore run inside testWidgets (or after
// TestWidgetsFlutterBinding.ensureInitialized()).

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/tokens/app_typography.dart';
import 'package:wandermeet_app/core/tokens/app_radius.dart';
import 'package:wandermeet_app/core/tokens/app_theme.dart';

void main() {
  // Pure const tests — no Flutter binding needed.
  group('AppColors constants', () {
    test('AppColors.ember is the brand color', () {
      expect(AppColors.ember, const Color(0xFFDC4F2C));
    });

    test('AppColors.teal is reserved for Open Today', () {
      expect(AppColors.teal, const Color(0xFF1F9985));
    });

    test('AppColors.iris is reserved for Trust + ID Verified', () {
      expect(AppColors.iris, const Color(0xFF6B4FB8));
    });
  });

  group('AppRadius / AppSpace / AppShadow constants', () {
    test('AppRadius.xxl equals 18', () {
      expect(AppRadius.xxl, 18.0);
    });

    test('AppSpace.lg equals 16', () {
      expect(AppSpace.lg, 16.0);
    });

    test('AppShadow.card is a non-empty list', () {
      expect(AppShadow.card, isNotEmpty);
    });
  });

  // Tests that access google_fonts must run inside testWidgets so that the
  // Flutter binding (ServicesBinding) is initialised before the font loader runs.
  group('AppText (google_fonts — requires binding)', () {
    testWidgets('AppText.body returns a TextStyle', (tester) async {
      expect(AppText.body, isA<TextStyle>());
    });

    testWidgets('AppText.title returns a TextStyle', (tester) async {
      expect(AppText.title, isA<TextStyle>());
    });

    testWidgets('AppText.displayLarge returns a TextStyle', (tester) async {
      expect(AppText.displayLarge, isA<TextStyle>());
    });

    testWidgets('AppText.monoLabel returns a TextStyle', (tester) async {
      expect(AppText.monoLabel, isA<TextStyle>());
    });
  });

  group('AppTheme (google_fonts — requires binding)', () {
    testWidgets('AppTheme.light is a ThemeData', (tester) async {
      expect(AppTheme.light, isA<ThemeData>());
    });

    testWidgets('AppTheme.dark is a ThemeData', (tester) async {
      expect(AppTheme.dark, isA<ThemeData>());
    });

    testWidgets('AppTheme.light primary color is ember', (tester) async {
      expect(AppTheme.light.colorScheme.primary, AppColors.ember);
    });

    testWidgets('AppTheme.dark brightness is dark', (tester) async {
      expect(AppTheme.dark.colorScheme.brightness, Brightness.dark);
    });
  });
}
