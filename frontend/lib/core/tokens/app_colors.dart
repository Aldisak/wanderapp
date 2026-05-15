// lib/theme/app_colors.dart
//
// Wander — design tokens (colors)
// Generated from the Wander Brand Book v1.3 — do not edit ad-hoc.
// Single source of truth for the entire app's color system.

import 'package:flutter/material.dart';

class AppColors {
  AppColors._();

  // ─── PRIMARY · EMBER ────────────────────────────────────────
  // The brand. Every CTA. Sunset terracotta — warm, energetic,
  // not safe. Filled-button background, link text, focus rings.
  static const ember = Color(0xFFDC4F2C);
  static const emberDeep = Color(0xFF8E2710); // dark surfaces, pressed
  static const emberLift = Color(0xFFF07B5A); // dark-mode primary
  static const emberTint = Color(0xFFFBE3DA); // hover, soft fills

  // ─── TEAL · the one signal ───────────────────────────────────
  // Reserved for "Open today" and the activity-today status dot.
  // NEVER use teal for primary buttons or links.
  static const teal = Color(0xFF1F9985);
  static const tealLift = Color(0xFF4FBFAC);
  static const tealTint = Color(0xFFDCEFEB);

  // ─── IRIS · trust + verified ONLY ────────────────────────────
  // Used exclusively for the Trust score badge and ID-Verified
  // chip. If you find yourself reaching for iris elsewhere, stop.
  static const iris = Color(0xFF6B4FB8);
  static const irisDeep = Color(0xFF3F2D7A);
  static const irisTint = Color(0xFFEEEAFA);
  // irisLift: dark-mode trust-badge foreground (COMPONENT_SPECS.md §5).
  // Added in UC-400 WI-2 round 2 — present in spec but absent from original
  // handoff source; same carve-out as WI-1 CardTheme/withOpacity token drift.
  static const irisLift = Color(0xFF9A82DC);

  // ─── SUN · sponsored ─────────────────────────────────────────
  // Used only on the Sponsored pill in place suggestions / Places.
  // Slot 3 only — never slot 1 or 2.
  static const sun = Color(0xFFE8A33A);
  static const sunTint = Color(0xFFFBEDD2);

  // ─── 3-STATE ACTIVITY DOTS ───────────────────────────────────
  // Coarse only. Filled green = today. Hollow yellow ring = 1–3d.
  // Nothing rendered for 4–7d. 7+d sinks below in discovery.
  // No red anywhere in the social surface.
  static const statusGreen = Color(0xFF3DA869);
  static const statusYellow = Color(0xFFD4A53C);

  // ─── ERROR · administrative only ─────────────────────────────
  // Never social. Report-a-concern affordance, validator errors.
  static const error = Color(0xFFB84545);

  // ─── AVATAR HUES · clay / sand / stone ──────────────────────
  // Muted earthy tones for the Avatar placeholder background.
  // clay: warm terracotta (uses white text). sand/stone: pale (use ink text).
  // Added in UC-400 WI-2 round 2 — appear in COMPONENT_SPECS.md §2 but were
  // absent from the original handoff source; carve-out matches WI-1 SDK drift.
  static const clay = Color(0xFFC97B5C);
  static const sand = Color(0xFFE8DAC2);
  static const stone = Color(0xFFC8BFAE);

  // ─── NEUTRALS · warm cream ───────────────────────────────────
  static const ink = Color(0xFF1F1A1A); // body text
  static const ink2 = Color(0xFF3F3633); // secondary text
  static const ink3 = Color(0xFF7A6E69); // captions, hints
  static const ink4 = Color(0xFFB0A49E); // disabled, dividers strong
  static const line = Color(0xFFE6DECF); // hairlines, borders
  static const paper = Color(0xFFF5EEDF); // app background
  static const paper2 = Color(0xFFFBF6EA); // raised paper, tinted sections
  static const white = Color(0xFFFFFFFF); // cards in light mode

  // ─── DARK MODE ───────────────────────────────────────────────
  static const dBg = Color(0xFF14110F);
  static const dBg2 = Color(0xFF221C19);
  static const dBg3 = Color(0xFF2D2622);
  static const dLine = Color(0xFF3F3633);
  static const dInk = Color(0xFFF4ECDD);
  static const dInk2 = Color(0xFFC8BBAB);
  static const dInk3 = Color(0xFF8A7C70);

  // ─── ROLE ALIASES — read these in feature code ───────────────
  // Prefer these in feature widgets. Swap underlying value once,
  // every screen updates.
  static const primary = ember;
  static const primaryOn = white;
  static const surface = white;
  static const surfaceMuted = paper2;
  static const background = paper;
  static const textPrimary = ink;
  static const textSecondary = ink2;
  static const textTertiary = ink3;
  static const trustBadgeFg = iris;
  static const trustBadgeBg = irisTint;
  static const openTodayFg = teal;
  static const openTodayBg = tealTint;
  static const sponsoredFg = Color(0xFF9C7019); // sun, darkened for AA on tint
  static const sponsoredBg = sunTint;
}
