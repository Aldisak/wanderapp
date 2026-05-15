import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/widgets/place_mock.dart';

void main() {
  group('PlaceMock', () {
    test('PlaceMock_constConstructor_compiles', () {
      const place = PlaceMock(
        name: 'Café Nero',
        emojiGlyph: '☕',
        rating: 4.8,
        distanceKm: 0.4,
        amenityPills: ['Strong wifi', 'Quiet'],
        meetupCount: 12,
      );
      expect(place.name, equals('Café Nero'));
      expect(place.emojiGlyph, equals('☕'));
      expect(place.rating, equals(4.8));
      expect(place.distanceKm, equals(0.4));
      expect(place.amenityPills, equals(['Strong wifi', 'Quiet']));
      expect(place.meetupCount, equals(12));
      expect(place.isSponsored, isFalse);
      expect(place.sponsoredPerk, isNull);
      expect(place.thumbnailUrl, isNull);
    });

    test('PlaceMock_sponsored_constConstructor_compiles', () {
      const place = PlaceMock(
        name: 'Starbucks',
        emojiGlyph: '⭐',
        rating: 4.2,
        distanceKm: 1.2,
        amenityPills: ['Fast wifi'],
        meetupCount: 3,
        isSponsored: true,
        sponsoredPerk: 'Free pastry on your first meetup',
      );
      expect(place.isSponsored, isTrue);
      expect(place.sponsoredPerk, equals('Free pastry on your first meetup'));
    });
  });
}
