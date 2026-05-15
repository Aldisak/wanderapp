// lib/app/showcase_gate.dart
//
// Routes to the design-system showcase in debug mode, or to a tiny
// placeholder in release builds. The ShowcasePage reference at file scope
// keeps the import graph valid in release — tree-shaking under --release
// drops the actual ShowcasePage widget instance (kDebugMode is const false
// in release, so the true branch is dead code from the compiler's perspective).

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import 'package:wandermeet_app/core/tokens/app_typography.dart';
import 'package:wandermeet_app/debug/showcase_page.dart';

/// Gate that serves the design-system [ShowcasePage] in debug mode and a
/// minimal placeholder in release builds.
class ShowcaseGate extends StatelessWidget {
  const ShowcaseGate({super.key});

  @override
  Widget build(BuildContext context) {
    if (kDebugMode) {
      return const ShowcasePage();
    }
    return Scaffold(
      body: Center(
        child: Text('Wander — design system loaded', style: AppText.body),
      ),
    );
  }
}
