// lib/theme/app_typography.dart
//
// Wander — type system.
//
// Two families, deliberately:
//   • Newsreader (literary serif, italic 300) — display, headlines,
//     and the rare invitation line. Adds warmth without bubbliness.
//   • Manrope (humanist sans) — every other surface in the product.
//     Geometric, even-tempered, open apertures.
//
// Both ship with full Cyrillic + Latin Extended so PL/CZ/DE/PT/ES
// diacritics never break. JetBrains Mono is used sparingly for
// taxonomy mono (section labels, "ABSENT BY DESIGN", "01 · …").
//
// Loading: use google_fonts (preferred — no asset wrangling) or
// embed the .ttf families under fonts/Newsreader, fonts/Manrope,
// fonts/JetBrainsMono and declare them in pubspec.yaml.

import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';
import 'app_colors.dart';

class AppText {
  AppText._();

  // serif — Newsreader (variable opsz, weight 300–700; italic supported)
  static TextStyle serif({
    double size = 16,
    FontWeight weight = FontWeight.w400,
    FontStyle style = FontStyle.normal,
    double height = 1.2,
    double letterSpacing = -0.01,
    Color color = AppColors.ink,
  }) => GoogleFonts.newsreader(
    fontSize: size,
    fontWeight: weight,
    fontStyle: style,
    height: height,
    letterSpacing: letterSpacing * size,
    color: color,
  );

  // sans — Manrope (200–800)
  static TextStyle sans({
    double size = 14,
    FontWeight weight = FontWeight.w400,
    double height = 1.5,
    double letterSpacing = -0.005,
    Color color = AppColors.ink,
  }) => GoogleFonts.manrope(
    fontSize: size,
    fontWeight: weight,
    height: height,
    letterSpacing: letterSpacing * size,
    color: color,
  );

  // mono — JetBrains Mono — section labels only
  static TextStyle mono({
    double size = 11,
    FontWeight weight = FontWeight.w500,
    double letterSpacing = 0.16,
    Color color = AppColors.ink3,
  }) => GoogleFonts.jetBrainsMono(
    fontSize: size,
    fontWeight: weight,
    letterSpacing: letterSpacing * size,
    color: color,
  );

  // ─── ROLE STYLES — use these in widgets ────────────────────
  // Display — Newsreader. Hero strings on Today/Profile/Review.
  static TextStyle get displayLarge => serif(
    size: 36,
    weight: FontWeight.w400,
    height: 1.05,
    letterSpacing: -0.025,
  );
  static TextStyle get displayMedium => serif(
    size: 32,
    weight: FontWeight.w400,
    height: 1.05,
    letterSpacing: -0.02,
  );
  static TextStyle get displaySmall => serif(
    size: 28,
    weight: FontWeight.w400,
    height: 1.1,
    letterSpacing: -0.02,
  );

  // Italic 300 display — for "with Marek?", "Eleven minutes."
  static TextStyle displayItalic({double size = 32, Color? color}) => serif(
    size: size,
    weight: FontWeight.w300,
    style: FontStyle.italic,
    height: 1.1,
    letterSpacing: -0.02,
    color: color ?? AppColors.ember,
  );

  // Headline — Newsreader, smaller. Section titles inside a screen.
  static TextStyle get headline => serif(
    size: 22,
    weight: FontWeight.w400,
    height: 1.2,
    letterSpacing: -0.015,
  );

  // Card name — Newsreader, "Sara", "Marek" on Discovery cards
  static TextStyle get cardName => serif(
    size: 22,
    weight: FontWeight.w400,
    height: 1.15,
    letterSpacing: -0.015,
  );

  // Title — Manrope, profile-page sub-titles, button labels, etc.
  static TextStyle get title => sans(
    size: 16,
    weight: FontWeight.w600,
    height: 1.3,
    letterSpacing: -0.01,
  );
  static TextStyle get titleSmall => sans(
    size: 15,
    weight: FontWeight.w600,
    height: 1.3,
    letterSpacing: -0.01,
  );

  // Body — Manrope 400
  static TextStyle get body => sans(
    size: 16,
    weight: FontWeight.w400,
    height: 1.5,
    letterSpacing: -0.005,
  );
  static TextStyle get bodySmall => sans(
    size: 14,
    weight: FontWeight.w400,
    height: 1.45,
    letterSpacing: -0.005,
  );

  // Caption — Manrope 500, distance/days-in-city/hint
  static TextStyle get caption => sans(
    size: 12,
    weight: FontWeight.w500,
    height: 1.4,
    letterSpacing: 0,
    color: AppColors.ink3,
  );

  // Pill — used on Open Today / hangout / trust pills
  static TextStyle get pill => sans(
    size: 11,
    weight: FontWeight.w600,
    height: 1.2,
    letterSpacing: -0.005,
  );

  // Mono — section taxonomy labels (always uppercase via String.toUpperCase())
  static TextStyle get monoLabel =>
      mono(size: 11, weight: FontWeight.w500, letterSpacing: 0.16);
  static TextStyle get monoTiny =>
      mono(size: 9, weight: FontWeight.w500, letterSpacing: 0.18);
}
