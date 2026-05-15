import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/tokens/app_radius.dart';
import 'package:wandermeet_app/core/tokens/app_typography.dart';
import 'package:wandermeet_app/core/widgets/place_mock.dart';
import 'package:wandermeet_app/core/widgets/sponsored_pill.dart';

/// Place suggestion row — used on the Places tab and inside the Invite composer.
///
/// Pass [selected] = true to render the trailing ember check chip
/// (Invite composer variant). The Places tab always uses [selected] = false.
class PlaceRow extends StatelessWidget {
  const PlaceRow({
    super.key,
    required this.place,
    this.selected = false,
    this.onTap,
  });

  final PlaceMock place;
  final bool selected;
  final VoidCallback? onTap;

  /// Format distance: always show km value as-is.
  String get _distanceLabel {
    return '${place.distanceKm} km';
  }

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _Thumb(place: place),
          const SizedBox(width: AppSpace.md + 2),
          Expanded(
            child: _Details(place: place, distanceLabel: _distanceLabel),
          ),
          if (selected) ...[
            const SizedBox(width: AppSpace.sm),
            const _CheckChip(),
          ],
        ],
      ),
    );
  }
}

class _Thumb extends StatelessWidget {
  const _Thumb({required this.place});

  final PlaceMock place;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 48,
      height: 48,
      decoration: BoxDecoration(
        color: place.isSponsored ? AppColors.sunTint : AppColors.paper2,
        borderRadius: AppRadius.all12,
      ),
      alignment: Alignment.center,
      child: Text(place.emojiGlyph, style: const TextStyle(fontSize: 24)),
    );
  }
}

class _Details extends StatelessWidget {
  const _Details({required this.place, required this.distanceLabel});

  final PlaceMock place;
  final String distanceLabel;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        // Name + optional sponsored pill
        Row(
          children: [
            Expanded(
              child: Text(
                place.name,
                style: AppText.titleSmall,
                overflow: TextOverflow.ellipsis,
              ),
            ),
            if (place.isSponsored) ...[
              const SizedBox(width: AppSpace.xxs),
              const SponsoredPill(),
            ],
          ],
        ),
        const SizedBox(height: AppSpace.xxs),
        // Rating + distance
        Row(
          children: [
            const Icon(Icons.star, color: AppColors.sun, size: 12),
            const SizedBox(width: 2),
            Text(place.rating.toString(), style: AppText.caption),
            Text(' · $distanceLabel', style: AppText.caption),
          ],
        ),
        const SizedBox(height: AppSpace.xxs),
        // Amenity pills + community pill
        Wrap(
          spacing: AppSpace.xs,
          runSpacing: AppSpace.xxs,
          children: [
            ...(place.amenityPills.map((a) => _AmenityChip(label: a))),
            _CommunityChip(meetupCount: place.meetupCount),
          ],
        ),
        // Sponsored perk line
        if (place.isSponsored && place.sponsoredPerk != null) ...[
          const SizedBox(height: AppSpace.xxs + 2),
          Text(
            '★ ${place.sponsoredPerk!}',
            style: AppText.pill.copyWith(
              color: AppColors.sponsoredFg,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ],
    );
  }
}

class _AmenityChip extends StatelessWidget {
  const _AmenityChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpace.xs,
        vertical: AppSpace.xxs / 2,
      ),
      decoration: BoxDecoration(
        color: AppColors.paper2,
        borderRadius: AppRadius.pillR,
        border: Border.all(color: AppColors.line),
      ),
      child: Text(label, style: AppText.pill.copyWith(color: AppColors.ink2)),
    );
  }
}

class _CommunityChip extends StatelessWidget {
  const _CommunityChip({required this.meetupCount});

  final int meetupCount;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpace.xs,
        vertical: AppSpace.xxs / 2,
      ),
      decoration: BoxDecoration(
        color: AppColors.paper2,
        borderRadius: AppRadius.pillR,
        border: Border.all(color: AppColors.line),
      ),
      child: Text(
        '$meetupCount Wander meetups',
        style: AppText.pill.copyWith(color: AppColors.ink2),
      ),
    );
  }
}

/// 22×22 ember circle with white check — only visible when [PlaceRow.selected] is true.
class _CheckChip extends StatelessWidget {
  const _CheckChip();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 22,
      height: 22,
      decoration: const BoxDecoration(
        color: AppColors.ember,
        shape: BoxShape.circle,
      ),
      child: const Icon(Icons.check, size: 14, color: AppColors.white),
    );
  }
}
