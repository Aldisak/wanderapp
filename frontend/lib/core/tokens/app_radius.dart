// lib/theme/app_radius.dart
//
// Wander — corner & spacing tokens. Generous, consistent, soft.

import 'package:flutter/material.dart';

class AppRadius {
  AppRadius._();

  // Used on cards, list rows, sheets
  static const double xs = 8;
  static const double sm = 10;
  static const double md = 12; // buttons, small cards
  static const double lg = 14; // input fields, place rows
  static const double xl = 16; // primary cards
  static const double xxl = 18; // discovery cards
  static const double pill = 9999;

  // Convenience BorderRadius
  static const BorderRadius all12 = BorderRadius.all(Radius.circular(md));
  static const BorderRadius all14 = BorderRadius.all(Radius.circular(lg));
  static const BorderRadius all16 = BorderRadius.all(Radius.circular(xl));
  static const BorderRadius all18 = BorderRadius.all(Radius.circular(xxl));
  static const BorderRadius pillR = BorderRadius.all(Radius.circular(pill));
}

class AppSpace {
  AppSpace._();

  // Vertical & horizontal rhythm
  static const double xxs = 4;
  static const double xs = 6;
  static const double sm = 8;
  static const double md = 12;
  static const double lg = 16; // screen padding default
  static const double xl = 20;
  static const double xxl = 24;
  static const double xxxl = 32;
}

class AppShadow {
  AppShadow._();

  // Card lift — barely visible, warm tint instead of cool gray.
  static const List<BoxShadow> card = [
    BoxShadow(color: Color(0x05000000), blurRadius: 1, offset: Offset(0, 1)),
    BoxShadow(color: Color(0x0A1F1A1A), blurRadius: 24, offset: Offset(0, 8)),
  ];

  // Primary CTA — ember glow
  static const List<BoxShadow> emberCta = [
    BoxShadow(color: Color(0x52DC4F2C), blurRadius: 24, offset: Offset(0, 8)),
  ];

  // Hover/pressed lift on Profile + Discovery cards in dark mode
  static const List<BoxShadow> cardDark = [
    BoxShadow(color: Color(0x40000000), blurRadius: 20, offset: Offset(0, 8)),
  ];
}
