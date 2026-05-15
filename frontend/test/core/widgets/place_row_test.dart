import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/widgets/place_mock.dart';
import 'package:wandermeet_app/core/widgets/place_row.dart';
import 'package:wandermeet_app/core/widgets/sponsored_pill.dart';

const _regularPlace = PlaceMock(
  name: 'Café Nero',
  emojiGlyph: '☕',
  rating: 4.8,
  distanceKm: 0.4,
  amenityPills: ['Strong wifi', 'Quiet'],
  meetupCount: 12,
);

const _sponsoredPlace = PlaceMock(
  name: 'Starbucks',
  emojiGlyph: '⭐',
  rating: 4.2,
  distanceKm: 1.2,
  amenityPills: ['Fast wifi'],
  meetupCount: 3,
  isSponsored: true,
  sponsoredPerk: 'Free pastry on your first meetup',
);

Widget _wrap(Widget child) {
  return MaterialApp(home: Scaffold(body: child));
}

void main() {
  group('PlaceRow', () {
    testWidgets('PlaceRow_RendersThumbNameRatingPillsCommunity_HappyPath', (
      tester,
    ) async {
      await tester.pumpWidget(_wrap(const PlaceRow(place: _regularPlace)));
      await tester.pump();

      expect(find.text('Café Nero'), findsOneWidget);
      expect(find.text('☕'), findsOneWidget);
      // rating
      expect(find.text('4.8'), findsOneWidget);
      // distance
      expect(find.textContaining('0.4 km'), findsOneWidget);
      // amenity pills
      expect(find.text('Strong wifi'), findsOneWidget);
      expect(find.text('Quiet'), findsOneWidget);
      // community pill
      expect(find.textContaining('12'), findsAtLeastNWidgets(1));
      expect(find.textContaining('Wander meetups'), findsOneWidget);
    });

    testWidgets('PlaceRow_Sponsored_RendersSponsoredPillAndPerk', (
      tester,
    ) async {
      await tester.pumpWidget(_wrap(const PlaceRow(place: _sponsoredPlace)));
      await tester.pump();

      expect(find.byType(SponsoredPill), findsOneWidget);
      expect(
        find.textContaining('Free pastry on your first meetup'),
        findsOneWidget,
      );
    });

    testWidgets('PlaceRow_Selected_RendersTrailingEmberCheckChip', (
      tester,
    ) async {
      await tester.pumpWidget(
        _wrap(const PlaceRow(place: _regularPlace, selected: true)),
      );
      await tester.pump();

      // The check circle should be rendered — look for an ember-colored container
      final containers = tester.widgetList<Container>(find.byType(Container));
      final hasEmberCircle = containers.any((c) {
        final deco = c.decoration;
        if (deco is BoxDecoration) {
          return deco.color == AppColors.ember && deco.shape == BoxShape.circle;
        }
        return false;
      });
      expect(hasEmberCircle, isTrue);
    });

    testWidgets('PlaceRow_NotSelected_OmitsTrailingCheckChip', (tester) async {
      await tester.pumpWidget(_wrap(const PlaceRow(place: _regularPlace)));
      await tester.pump();

      // Without selected=true, no ember circle
      final containers = tester.widgetList<Container>(find.byType(Container));
      final hasEmberCircle = containers.any((c) {
        final deco = c.decoration;
        if (deco is BoxDecoration) {
          return deco.color == AppColors.ember && deco.shape == BoxShape.circle;
        }
        return false;
      });
      expect(hasEmberCircle, isFalse);
    });
  });
}
