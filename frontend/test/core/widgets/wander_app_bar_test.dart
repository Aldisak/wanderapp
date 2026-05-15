import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/widgets/wander_app_bar.dart';
import 'package:wandermeet_app/core/widgets/wander_back_button.dart'; // used by WanderAppBar_BackLabel_RendersWanderBackButton

void main() {
  group('WanderAppBar', () {
    testWidgets('WanderAppBar_BackLabel_RendersWanderBackButton', (
      tester,
    ) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            appBar: WanderAppBar(backLabel: 'Today'),
            body: SizedBox.shrink(),
          ),
        ),
      );
      await tester.pump();

      expect(find.byType(WanderBackButton), findsOneWidget);
      expect(find.text('Today'), findsOneWidget);
    });

    testWidgets('WanderAppBar_NoBackLabel_OmitsBackButton', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(appBar: WanderAppBar(), body: SizedBox.shrink()),
        ),
      );
      await tester.pump();

      expect(find.byType(WanderBackButton), findsNothing);
    });

    testWidgets('WanderAppBar_WithTrailing_RendersTrailingWidget', (
      tester,
    ) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            appBar: WanderAppBar(
              trailing: Icon(Icons.info_outline, key: Key('trailing_icon')),
            ),
            body: SizedBox.shrink(),
          ),
        ),
      );
      await tester.pump();

      expect(find.byKey(const Key('trailing_icon')), findsOneWidget);
    });

    testWidgets('WanderAppBar_preferredSize_isToolbarHeight', (tester) async {
      const appBar = WanderAppBar();
      expect(appBar.preferredSize.height, equals(kToolbarHeight));
    });
  });
}
