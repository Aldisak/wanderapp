// lib/app/wandermeet_app.dart
//
// Root application widget for WanderMeet.
//
// Uses MaterialApp (NOT MaterialApp.router) — GoRouter lands in UC-401.
// Promote to ConsumerWidget when UC-401 wires goRouterProvider and the
// body reads from it.

import 'package:flutter/material.dart';

import 'package:wandermeet_app/core/tokens/app_theme.dart';
import 'package:wandermeet_app/app/showcase_gate.dart';

/// The root widget of the WanderMeet application.
///
/// Configures Material 3 theming via [AppTheme] and delegates to
/// [ShowcaseGate] for the initial screen until UC-401 wires routing.
class WanderMeetApp extends StatelessWidget {
  const WanderMeetApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Wander',
      theme: AppTheme.light,
      darkTheme: AppTheme.dark,
      themeMode: ThemeMode.system,
      home: const ShowcaseGate(),
    );
  }
}
