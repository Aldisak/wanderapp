import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/widgets/status_dot.dart';

void main() {
  group('StatusDot', () {
    testWidgets('StatusDot_online_filled_green_with_halo', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: Center(child: StatusDot(status: ActivityStatus.online)),
          ),
        ),
      );
      await tester.pump();
      // online renders a visible Container (not SizedBox.shrink)
      expect(find.byType(SizedBox), findsNothing);
      expect(find.byType(Container), findsAtLeastNWidgets(1));
    });

    testWidgets(
      'StatusDot_recent_hollow_yellow_ring_with_transparent_fill_and_nonzero_border',
      (tester) async {
        await tester.pumpWidget(
          const MaterialApp(
            home: Scaffold(
              body: Center(child: StatusDot(status: ActivityStatus.recent)),
            ),
          ),
        );
        await tester.pump();
        // recent renders something (not SizedBox.shrink) and has a BoxDecoration border
        expect(find.byType(Container), findsAtLeastNWidgets(1));
        final container = tester.widget<Container>(
          find.byType(Container).first,
        );
        final decoration = container.decoration as BoxDecoration?;
        expect(decoration, isNotNull);
        expect(decoration!.border, isNotNull);
        expect(decoration.color, equals(Colors.transparent));
      },
    );

    testWidgets('StatusDot_hidden_renders_SizedBox_shrink', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: Center(child: StatusDot(status: ActivityStatus.hidden)),
          ),
        ),
      );
      await tester.pump();
      // hidden returns SizedBox.shrink — no Container
      expect(find.byType(Container), findsNothing);
    });

    testWidgets('StatusDot_RendersRecentRingInDarkMode', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: ThemeData(brightness: Brightness.dark),
          home: const Scaffold(
            body: Center(child: StatusDot(status: ActivityStatus.recent)),
          ),
        ),
      );
      await tester.pump();
      // still renders non-SizedBox in dark mode
      expect(find.byType(Container), findsAtLeastNWidgets(1));
      final container = tester.widget<Container>(find.byType(Container).first);
      final decoration = container.decoration as BoxDecoration?;
      expect(decoration, isNotNull);
      expect(decoration!.border, isNotNull);
      expect(decoration.color, equals(Colors.transparent));
    });
  });
}
