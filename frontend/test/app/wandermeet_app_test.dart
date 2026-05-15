// test/app/wandermeet_app_test.dart
//
// WI-1 tests: WanderMeetApp builds, ShowcaseGate selects correct child.

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/app/wandermeet_app.dart';
import 'package:wandermeet_app/app/showcase_gate.dart';
import 'package:wandermeet_app/debug/showcase_page.dart';

void main() {
  group('WanderMeetApp', () {
    testWidgets('builds without throwing', (tester) async {
      await tester.pumpWidget(const WanderMeetApp());
      // If we reach here without exception the widget tree was built.
      expect(find.byType(WanderMeetApp), findsOneWidget);
    });

    testWidgets('contains a MaterialApp', (tester) async {
      await tester.pumpWidget(const WanderMeetApp());
      expect(find.byType(MaterialApp), findsOneWidget);
    });
  });

  group('ShowcaseGate', () {
    testWidgets('shows ShowcasePage in debug mode', (tester) async {
      // kDebugMode is always true under `flutter test`.
      await tester.pumpWidget(const MaterialApp(home: ShowcaseGate()));
      await tester.pump();
      expect(
        kDebugMode,
        isTrue,
        reason:
            'This test only covers the debug path; '
            'release path is verified by flutter analyze + dart compile.',
      );
      expect(find.byType(ShowcasePage), findsOneWidget);
    });

    testWidgets('ShowcaseGate is a const StatelessWidget', (tester) async {
      const gate = ShowcaseGate();
      expect(gate, isA<StatelessWidget>());
    });
  });
}
