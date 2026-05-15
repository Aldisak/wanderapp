import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/widgets/wander_back_button.dart';

class _PopSpy extends NavigatorObserver {
  int popCount = 0;

  @override
  void didPop(Route<dynamic> route, Route<dynamic>? previousRoute) {
    popCount++;
    super.didPop(route, previousRoute);
  }
}

class _SubRouteScreen extends StatelessWidget {
  const _SubRouteScreen();

  @override
  Widget build(BuildContext context) {
    return Scaffold(body: WanderBackButton(label: 'Back'));
  }
}

void main() {
  group('WanderBackButton', () {
    testWidgets('WanderBackButton_DefaultPath_PopsRouteViaMaybePop', (
      tester,
    ) async {
      final spy = _PopSpy();

      await tester.pumpWidget(
        MaterialApp(
          navigatorObservers: [spy],
          home: Builder(
            builder: (context) => Scaffold(
              body: ElevatedButton(
                onPressed: () {
                  Navigator.push(
                    context,
                    MaterialPageRoute<void>(
                      builder: (_) => const _SubRouteScreen(),
                    ),
                  );
                },
                child: const Text('Go'),
              ),
            ),
          ),
        ),
      );

      // Navigate to sub-route.
      await tester.tap(find.text('Go'));
      await tester.pumpAndSettle();

      // Sub-route is on screen.
      expect(find.byType(_SubRouteScreen), findsOneWidget);

      // Tap WanderBackButton WITHOUT onTap override — exercises default path.
      await tester.tap(find.byType(WanderBackButton));
      await tester.pumpAndSettle();

      // Sub-route should be gone; pop was fired via Navigator.maybePop.
      expect(find.byType(_SubRouteScreen), findsNothing);
      expect(spy.popCount, 1);
    });

    testWidgets(
      'WanderBackButton_OnTapOverride_FiresOverrideInsteadOfMaybePop',
      (tester) async {
        var overrideFired = false;
        final spy = _PopSpy();

        await tester.pumpWidget(
          MaterialApp(
            navigatorObservers: [spy],
            home: Builder(
              builder: (context) => Scaffold(
                body: ElevatedButton(
                  onPressed: () {
                    Navigator.push(
                      context,
                      MaterialPageRoute<void>(
                        builder: (_) => Scaffold(
                          body: WanderBackButton(
                            label: 'Back',
                            onTap: () => overrideFired = true,
                          ),
                        ),
                      ),
                    );
                  },
                  child: const Text('Go'),
                ),
              ),
            ),
          ),
        );

        await tester.tap(find.text('Go'));
        await tester.pumpAndSettle();

        await tester.tap(find.byType(WanderBackButton));
        await tester.pumpAndSettle();

        // Override callback was invoked.
        expect(overrideFired, isTrue);
        // Navigator.maybePop was NOT called (spy sees no pop from our override).
        expect(spy.popCount, 0);
      },
    );

    testWidgets('WanderBackButton_renders_label_and_chevron', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(body: WanderBackButton(label: 'Today')),
        ),
      );
      await tester.pump();

      expect(find.text('Today'), findsOneWidget);
      expect(find.byIcon(Icons.chevron_left), findsOneWidget);
    });
  });
}
