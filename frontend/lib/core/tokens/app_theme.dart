// lib/theme/app_theme.dart
//
// Wander — Material 3 ThemeData for light + dark.
// Wrap your MaterialApp in MaterialApp(theme: AppTheme.light, darkTheme: AppTheme.dark).
//
// Notes
//   • We intentionally use Material 3 (useMaterial3: true) but override
//     most surfaces ourselves — Material defaults are too cool/blue.
//   • All large numeric values come from app_radius.dart / app_typography.dart.
//   • Buttons fall through to ElevatedButton everywhere — no FAB, no outlined-only.

import 'package:flutter/material.dart';
import 'app_colors.dart';
import 'app_typography.dart';
import 'app_radius.dart';

class AppTheme {
  AppTheme._();

  static ThemeData get light => _build(brightness: Brightness.light);
  static ThemeData get dark => _build(brightness: Brightness.dark);

  static ThemeData _build({required Brightness brightness}) {
    final isDark = brightness == Brightness.dark;

    final scheme = ColorScheme(
      brightness: brightness,
      primary: AppColors.ember,
      onPrimary: AppColors.white,
      primaryContainer: AppColors.emberTint,
      onPrimaryContainer: AppColors.emberDeep,
      secondary: AppColors.teal,
      onSecondary: AppColors.white,
      secondaryContainer: AppColors.tealTint,
      onSecondaryContainer: AppColors.teal,
      tertiary: AppColors.iris,
      onTertiary: AppColors.white,
      tertiaryContainer: AppColors.irisTint,
      onTertiaryContainer: AppColors.irisDeep,
      error: AppColors.error,
      onError: AppColors.white,
      surface: isDark ? AppColors.dBg2 : AppColors.white,
      onSurface: isDark ? AppColors.dInk : AppColors.ink,
      surfaceContainerHighest: isDark ? AppColors.dBg3 : AppColors.paper2,
      outline: isDark ? AppColors.dLine : AppColors.line,
      outlineVariant: isDark ? AppColors.dLine : AppColors.line,
    );

    return ThemeData(
      useMaterial3: true,
      brightness: brightness,
      colorScheme: scheme,
      scaffoldBackgroundColor: isDark ? AppColors.dBg : AppColors.paper,
      canvasColor: isDark ? AppColors.dBg : AppColors.paper,
      splashFactory: InkRipple.splashFactory,

      // Buttons — primary ember filled, 54h, 14r
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: AppColors.ember,
          foregroundColor: AppColors.white,
          minimumSize: const Size.fromHeight(54),
          shape: const RoundedRectangleBorder(borderRadius: AppRadius.all14),
          textStyle: AppText.title,
          elevation: 0,
          shadowColor: const Color(0x52DC4F2C),
        ),
      ),

      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: AppColors.ember,
          side: const BorderSide(color: AppColors.ember, width: 1.5),
          minimumSize: const Size.fromHeight(54),
          shape: const RoundedRectangleBorder(borderRadius: AppRadius.all14),
          textStyle: AppText.title,
        ),
      ),

      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: AppColors.ember,
          textStyle: AppText.titleSmall,
        ),
      ),

      // Cards — flat, 18r, soft warm shadow handled per-widget
      cardTheme: CardThemeData(
        color: isDark ? AppColors.dBg2 : AppColors.white,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        shape: const RoundedRectangleBorder(borderRadius: AppRadius.all18),
        margin: EdgeInsets.zero,
      ),

      // Input — hairline outline, soft fill
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: isDark ? AppColors.dBg2 : AppColors.white,
        contentPadding: const EdgeInsets.fromLTRB(14, 14, 14, 14),
        hintStyle: AppText.body.copyWith(color: AppColors.ink3),
        labelStyle: AppText.bodySmall.copyWith(color: AppColors.ink3),
        border: OutlineInputBorder(
          borderRadius: AppRadius.all14,
          borderSide: BorderSide(
            color: isDark ? AppColors.dLine : AppColors.line,
          ),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: AppRadius.all14,
          borderSide: BorderSide(
            color: isDark ? AppColors.dLine : AppColors.line,
          ),
        ),
        focusedBorder: const OutlineInputBorder(
          borderRadius: AppRadius.all14,
          borderSide: BorderSide(color: AppColors.ember, width: 1.5),
        ),
      ),

      // App bar — paper, no elevation, transparent scroll
      appBarTheme: AppBarTheme(
        backgroundColor: isDark ? AppColors.dBg : AppColors.paper,
        surfaceTintColor: Colors.transparent,
        foregroundColor: isDark ? AppColors.dInk : AppColors.ink,
        elevation: 0,
        scrolledUnderElevation: 0,
        centerTitle: false,
        titleTextStyle: AppText.title.copyWith(
          color: isDark ? AppColors.dInk : AppColors.ink,
        ),
      ),

      // Bottom nav — glassy, ember active, mono labels
      bottomNavigationBarTheme: BottomNavigationBarThemeData(
        backgroundColor: (isDark ? AppColors.dBg2 : AppColors.white).withValues(
          alpha: 0.88,
        ),
        selectedItemColor: AppColors.ember,
        unselectedItemColor: isDark ? AppColors.dInk3 : AppColors.ink3,
        showSelectedLabels: true,
        showUnselectedLabels: true,
        type: BottomNavigationBarType.fixed,
        selectedLabelStyle: AppText.pill,
        unselectedLabelStyle: AppText.pill,
      ),

      // Snackbar — ink-dark
      snackBarTheme: SnackBarThemeData(
        backgroundColor: AppColors.ink,
        contentTextStyle: AppText.bodySmall.copyWith(color: AppColors.paper),
        behavior: SnackBarBehavior.floating,
        shape: const RoundedRectangleBorder(borderRadius: AppRadius.all14),
      ),

      dividerTheme: DividerThemeData(
        color: isDark ? AppColors.dLine : AppColors.line,
        thickness: 1,
        space: 1,
      ),

      // Hairline iOS-feel hairline ripple
      pageTransitionsTheme: const PageTransitionsTheme(
        builders: {
          TargetPlatform.iOS: CupertinoPageTransitionsBuilder(),
          TargetPlatform.android: CupertinoPageTransitionsBuilder(),
        },
      ),
    );
  }
}
