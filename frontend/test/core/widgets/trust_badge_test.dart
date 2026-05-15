import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/widgets/trust_badge.dart';

void main() {
  group('TrustBadge', () {
    testWidgets('TrustBadge_trusted_requires_score', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: Center(
              child: TrustBadge(kind: TrustBadgeKind.trusted, score: 92),
            ),
          ),
        ),
      );
      await tester.pump();
      expect(find.textContaining('Trusted'), findsOneWidget);
      expect(find.textContaining('92'), findsOneWidget);
    });

    testWidgets('TrustBadge_idVerified_label_is_ID_Verified', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: Center(child: TrustBadge(kind: TrustBadgeKind.idVerified)),
          ),
        ),
      );
      await tester.pump();
      expect(find.text('ID Verified'), findsOneWidget);
    });

    testWidgets('TrustBadge_const_constructor_compiles', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: Column(
              children: [
                TrustBadge(kind: TrustBadgeKind.trusted, score: 80),
                TrustBadge(kind: TrustBadgeKind.idVerified),
              ],
            ),
          ),
        ),
      );
      expect(find.byType(TrustBadge), findsNWidgets(2));
    });

    test('TrustBadge_TrustedWithoutScore_FailsAssertion', () {
      expect(
        () => TrustBadge(kind: TrustBadgeKind.trusted),
        throwsAssertionError,
      );
    });

    test('TrustBadge_IdVerifiedWithoutScore_DoesNotFail', () {
      // idVerified does not require a score — should not throw.
      expect(
        () => const TrustBadge(kind: TrustBadgeKind.idVerified),
        returnsNormally,
      );
    });
  });
}
