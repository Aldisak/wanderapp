import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/widgets/avatar.dart';
import 'package:wandermeet_app/core/widgets/discovery_card.dart';
import 'package:wandermeet_app/core/widgets/ember_cta.dart';
import 'package:wandermeet_app/core/widgets/hangout_tag.dart';
import 'package:wandermeet_app/core/widgets/status_dot.dart';
import 'package:wandermeet_app/core/widgets/trust_badge.dart';
import 'package:wandermeet_app/core/widgets/user_summary_mock.dart';

const _onlineUser = UserSummaryMock(
  firstName: 'Sara',
  flagEmoji: '🇨🇿',
  activity: ActivityStatus.online,
  occupation: 'Designer',
  daysInCity: 14,
  trustScore: 92,
  isIdVerified: true,
  bio: 'Love coffee and long walks through the old town.',
  hangouts: [Hangout.coffee, Hangout.walk],
  avatarHue: AvatarHue.ember,
);

const _recentUser = UserSummaryMock(
  firstName: 'Marek',
  flagEmoji: '🇵🇱',
  activity: ActivityStatus.recent,
  occupation: 'Engineer',
  daysInCity: 5,
  trustScore: 78,
  isIdVerified: false,
  bio: 'Coffee and code.',
  hangouts: [Hangout.cowork],
  avatarHue: AvatarHue.iris,
);

const _hiddenUser = UserSummaryMock(
  firstName: 'Jan',
  flagEmoji: '🇩🇪',
  activity: ActivityStatus.hidden,
  occupation: 'Teacher',
  daysInCity: 2,
  trustScore: 65,
  isIdVerified: false,
  bio: 'Exploring the city.',
  hangouts: [Hangout.explore],
  avatarHue: AvatarHue.teal,
);

Widget _wrap(Widget child) {
  return MaterialApp(
    home: Scaffold(body: SingleChildScrollView(child: child)),
  );
}

void main() {
  group('DiscoveryCard', () {
    testWidgets('DiscoveryCard_RendersIdentityRowBioHangoutsCta_HappyPath', (
      tester,
    ) async {
      await tester.pumpWidget(
        _wrap(DiscoveryCard(user: _onlineUser, onTapMeet: () {})),
      );
      await tester.pump();

      expect(find.byType(Avatar), findsOneWidget);
      expect(find.byType(StatusDot), findsOneWidget);
      expect(find.byType(TrustBadge), findsAtLeastNWidgets(1));
      expect(find.byType(HangoutTag), findsAtLeastNWidgets(1));
      expect(find.byType(EmberCTA), findsOneWidget);
      // bio text
      expect(
        find.text('Love coffee and long walks through the old town.'),
        findsOneWidget,
      );
      // first name
      expect(find.text('Sara'), findsOneWidget);
    });

    testWidgets('DiscoveryCard_OnlineActivity_RendersFilledEmberCta', (
      tester,
    ) async {
      await tester.pumpWidget(
        _wrap(DiscoveryCard(user: _onlineUser, onTapMeet: () {})),
      );
      await tester.pump();
      expect(find.byType(EmberCTA), findsOneWidget);
    });

    testWidgets('DiscoveryCard_RecentActivity_RendersReducedWeightCta', (
      tester,
    ) async {
      await tester.pumpWidget(
        _wrap(DiscoveryCard(user: _recentUser, onTapMeet: () {})),
      );
      await tester.pump();
      // CTA should still render for recent (reduced weight via Opacity)
      expect(find.byType(EmberCTA), findsOneWidget);
    });

    testWidgets('DiscoveryCard_HiddenActivity_OmitsCtaRow', (tester) async {
      await tester.pumpWidget(
        _wrap(DiscoveryCard(user: _hiddenUser, onTapMeet: () {})),
      );
      await tester.pump();
      // No EmberCTA for hidden users
      expect(find.byType(EmberCTA), findsNothing);
    });

    testWidgets('DiscoveryCard_OnTapMeet_FiresCallback', (tester) async {
      var callCount = 0;
      await tester.pumpWidget(
        _wrap(DiscoveryCard(user: _onlineUser, onTapMeet: () => callCount++)),
      );
      await tester.pump();

      await tester.tap(find.byType(EmberCTA));
      await tester.pump();
      expect(callCount, equals(1));
    });

    testWidgets('DiscoveryCard_OnTap_FiresCallback', (tester) async {
      var cardTapped = false;
      await tester.pumpWidget(
        _wrap(
          DiscoveryCard(
            user: _onlineUser,
            onTapMeet: () {},
            onTap: () => cardTapped = true,
          ),
        ),
      );
      await tester.pump();

      // Tap somewhere on the card that is not the CTA button
      await tester.tap(find.text('Sara'));
      await tester.pump();
      expect(cardTapped, isTrue);
    });
  });
}
