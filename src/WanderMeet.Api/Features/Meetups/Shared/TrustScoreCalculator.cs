using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Meetups.Shared;

/// <summary>Stateless helper that computes a user's trust score from aggregated review counts.</summary>
internal static class TrustScoreCalculator
{
    /// <summary>
    /// Computes the trust score and meetup count from aggregated review signal counts.
    /// Formula: <c>base = didMeetCount*6 + feltSafeCount*4 + wouldMeetAgainCount*3 + goodConvoCount*2</c>.
    /// The score is clamped to [<see cref="ValidationConstants.TrustScoreMin"/>, <see cref="ValidationConstants.TrustScoreMax"/>].
    /// <c>MeetupCount</c> equals <paramref name="didMeetCount"/> (reviews where the meetup actually happened).
    /// </summary>
    /// <param name="didMeetCount">Number of reviews where the reviewer confirmed the meetup happened.</param>
    /// <param name="feltSafeCount">Number of reviews where the reviewer felt safe.</param>
    /// <param name="wouldMeetAgainCount">Number of reviews where the reviewer would meet again.</param>
    /// <param name="goodConvoCount">Number of reviews where the reviewer had a good conversation.</param>
    /// <returns>A tuple of (TrustScore clamped to 0–100, MeetupCount equal to didMeetCount).</returns>
    public static (int TrustScore, int MeetupCount) Compute(
        int didMeetCount,
        int feltSafeCount,
        int wouldMeetAgainCount,
        int goodConvoCount)
    {
        var baseScore = (didMeetCount * 6) + (feltSafeCount * 4) + (wouldMeetAgainCount * 3) + (goodConvoCount * 2);
        var trustScore = Math.Clamp(baseScore, ValidationConstants.TrustScoreMin, ValidationConstants.TrustScoreMax);
        return (trustScore, didMeetCount);
    }
}
