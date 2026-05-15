import 'package:flutter_test/flutter_test.dart';
import 'package:wandermeet_app/core/widgets/avatar.dart';
import 'package:wandermeet_app/core/widgets/hangout_tag.dart';
import 'package:wandermeet_app/core/widgets/status_dot.dart';
import 'package:wandermeet_app/core/widgets/user_summary_mock.dart';

void main() {
  group('UserSummaryMock', () {
    test('UserSummaryMock_constConstructor_compiles', () {
      const user = UserSummaryMock(
        firstName: 'Sara',
        flagEmoji: '🇨🇿',
        activity: ActivityStatus.online,
        occupation: 'Designer',
        daysInCity: 14,
        trustScore: 92,
        isIdVerified: true,
        bio: 'Love coffee and long walks.',
        hangouts: [Hangout.coffee, Hangout.walk],
        avatarHue: AvatarHue.ember,
      );
      expect(user.firstName, equals('Sara'));
      expect(user.flagEmoji, equals('🇨🇿'));
      expect(user.activity, equals(ActivityStatus.online));
      expect(user.occupation, equals('Designer'));
      expect(user.daysInCity, equals(14));
      expect(user.trustScore, equals(92));
      expect(user.isIdVerified, isTrue);
      expect(user.bio, equals('Love coffee and long walks.'));
      expect(user.hangouts, equals([Hangout.coffee, Hangout.walk]));
      expect(user.avatarHue, equals(AvatarHue.ember));
      expect(user.imageUrl, isNull);
    });

    test('UserSummaryMock_withImageUrl_compiles', () {
      const user = UserSummaryMock(
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
        imageUrl: 'https://example.com/photo.jpg',
      );
      expect(user.imageUrl, equals('https://example.com/photo.jpg'));
    });
  });
}
