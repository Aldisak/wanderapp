// TODO(UC-507): replace with the real Place entity from features/places.
// Mock kept here only so WI-3 composites and the WI-4 showcase compile in isolation.

/// Temporary stand-in for the real Place domain entity (UC-507).
/// All fields are final and the constructor is const.
class PlaceMock {
  const PlaceMock({
    required this.name,
    required this.emojiGlyph,
    required this.rating,
    required this.distanceKm,
    required this.amenityPills,
    required this.meetupCount,
    this.isSponsored = false,
    this.sponsoredPerk,
    this.thumbnailUrl,
  });

  final String name;
  final String emojiGlyph;
  final double rating;
  final double distanceKm;
  final List<String> amenityPills;
  final int meetupCount;
  final bool isSponsored;
  final String? sponsoredPerk;
  final String? thumbnailUrl;
}
