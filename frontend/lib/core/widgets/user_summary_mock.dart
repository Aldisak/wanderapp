// TODO(UC-405): replace with the real UserSummary entity from features/profile.
// Mock kept here only so WI-3 composites and the WI-4 showcase compile in isolation.

import 'package:wandermeet_app/core/widgets/avatar.dart';
import 'package:wandermeet_app/core/widgets/hangout_tag.dart';
import 'package:wandermeet_app/core/widgets/status_dot.dart';

/// Temporary stand-in for the real UserSummary domain entity (UC-405).
/// All fields are final and the constructor is const.
class UserSummaryMock {
  const UserSummaryMock({
    required this.firstName,
    required this.flagEmoji,
    required this.activity,
    required this.occupation,
    required this.daysInCity,
    required this.trustScore,
    required this.isIdVerified,
    required this.bio,
    required this.hangouts,
    required this.avatarHue,
    this.imageUrl,
  });

  final String firstName;
  final String flagEmoji;
  final ActivityStatus activity;
  final String occupation;
  final int daysInCity;
  final int trustScore;
  final bool isIdVerified;
  final String bio;
  final List<Hangout> hangouts;
  final AvatarHue avatarHue;
  final String? imageUrl;
}
