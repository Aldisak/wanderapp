using FluentAssertions;
using WanderMeet.Api.Features.Meetups.Shared;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Meetups.Shared;

/// <summary>Unit tests for <see cref="TrustScoreCalculator"/>.</summary>
public class TrustScoreCalculatorTests
{
    /// <summary>(0,0,0,0) -> (TrustScore=0, MeetupCount=0).</summary>
    [Fact]
    public void Compute_NoReviews_ReturnsZeroAndZero()
    {
        var (trustScore, meetupCount) = TrustScoreCalculator.Compute(0, 0, 0, 0);

        trustScore.Should().Be(0);
        meetupCount.Should().Be(0);
    }

    /// <summary>(1,1,1,1) -> base = 6+4+3+2 = 15, meetupCount = 1.</summary>
    [Fact]
    public void Compute_OneAllPositiveDidMeetReview_Returns15AndMeetupCount1()
    {
        var (trustScore, meetupCount) = TrustScoreCalculator.Compute(1, 1, 1, 1);

        trustScore.Should().Be(15);
        meetupCount.Should().Be(1);
    }

    /// <summary>(0,1,1,1) -> didMeet=false so meetupCount=0, base = 0*6+1*4+1*3+1*2 = 9.</summary>
    [Fact]
    public void Compute_DidMeetFalseReview_DoesNotIncrementMeetupCount()
    {
        var (trustScore, meetupCount) = TrustScoreCalculator.Compute(0, 1, 1, 1);

        trustScore.Should().Be(9);
        meetupCount.Should().Be(0);
    }

    /// <summary>(50,50,50,50) -> base = 50*6+50*4+50*3+50*2 = 750, clamped to 100.</summary>
    [Fact]
    public void Compute_FiftyAllPositiveDidMeetReviews_ClampsAt100()
    {
        var (trustScore, meetupCount) = TrustScoreCalculator.Compute(50, 50, 50, 50);

        trustScore.Should().Be(100);
        meetupCount.Should().Be(50);
    }

    /// <summary>(1,1,0,0) -> base = 1*6+1*4+0+0 = 10, meetupCount = 1.</summary>
    [Fact]
    public void Compute_OnlyFeltSafeTrue_ReturnsExpectedFormula()
    {
        var (trustScore, meetupCount) = TrustScoreCalculator.Compute(1, 1, 0, 0);

        trustScore.Should().Be(10);
        meetupCount.Should().Be(1);
    }
}
