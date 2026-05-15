import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/widgets/wander_bottom_nav.dart';

Widget _wrap(Widget child) {
  return MaterialApp(home: Scaffold(body: child));
}

void main() {
  group('WanderBottomNav', () {
    testWidgets('WanderBottomNav_Renders4Items_ActiveItemHasEmberColor', (
      tester,
    ) async {
      await tester.pumpWidget(
        _wrap(WanderBottomNav(currentIndex: 0, onChange: (_) {})),
      );
      await tester.pump();

      // Verify 4 nav item labels are present
      expect(find.text('Today'), findsOneWidget);
      expect(find.text('Places'), findsOneWidget);
      expect(find.text('Invites'), findsOneWidget);
      expect(find.text('You'), findsOneWidget);

      // Active item (Today, index 0) label should use ember color
      final todayText = tester.widget<Text>(find.text('Today'));
      expect(todayText.style?.color, equals(AppColors.ember));
    });

    testWidgets('WanderBottomNav_IncomingUnreadCount_RendersEmberBadge', (
      tester,
    ) async {
      await tester.pumpWidget(
        _wrap(
          WanderBottomNav(
            currentIndex: 0,
            onChange: (_) {},
            incomingUnreadCount: 5,
          ),
        ),
      );
      await tester.pump();

      // Badge container with ember background
      final containers = tester.widgetList<Container>(find.byType(Container));
      final hasBadge = containers.any((c) {
        final deco = c.decoration;
        if (deco is BoxDecoration) {
          // badge is ember, NOT red
          return deco.color == AppColors.ember && deco.borderRadius != null;
        }
        return false;
      });
      expect(hasBadge, isTrue);

      // Badge should NOT be red (error color)
      final hasRedBadge = containers.any((c) {
        final deco = c.decoration;
        if (deco is BoxDecoration) {
          return deco.color == AppColors.error;
        }
        return false;
      });
      expect(hasRedBadge, isFalse);

      // Badge shows count
      expect(find.text('5'), findsOneWidget);
    });

    testWidgets('WanderBottomNav_OverflowCount_ShowsPlus99', (tester) async {
      await tester.pumpWidget(
        _wrap(
          WanderBottomNav(
            currentIndex: 0,
            onChange: (_) {},
            incomingUnreadCount: 150,
          ),
        ),
      );
      await tester.pump();

      expect(find.text('99+'), findsOneWidget);
    });

    testWidgets('WanderBottomNav_OnTap_FiresOnChange', (tester) async {
      var selectedIndex = 0;
      await tester.pumpWidget(
        StatefulBuilder(
          builder: (context, setState) => MaterialApp(
            home: Scaffold(
              body: WanderBottomNav(
                currentIndex: selectedIndex,
                onChange: (i) => setState(() => selectedIndex = i),
              ),
            ),
          ),
        ),
      );
      await tester.pump();

      await tester.tap(find.text('Places'));
      await tester.pump();
      expect(selectedIndex, equals(1));
    });

    testWidgets('WanderBottomNav_NoUnreadCount_NoBadge', (tester) async {
      await tester.pumpWidget(
        _wrap(WanderBottomNav(currentIndex: 2, onChange: (_) {})),
      );
      await tester.pump();

      // No badge text (single digit or 99+)
      final containers = tester.widgetList<Container>(find.byType(Container));
      // Count of 14×14 ember badge circles should be zero
      final badgeContainers = containers.where((c) {
        final deco = c.decoration;
        if (deco is BoxDecoration && deco.color == AppColors.ember) {
          return c.constraints?.maxWidth == 14;
        }
        return false;
      });
      expect(badgeContainers.isEmpty, isTrue);
    });
  });
}
