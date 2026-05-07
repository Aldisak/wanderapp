using WanderMeet.Api.Features.Discovery.Feed;
using Xunit;
using FluentAssertions;

namespace WanderMeet.Api.UnitTests.Features.Discovery.Feed;

/// <summary>Unit tests for <see cref="DiscoveryCursor"/> encode/decode round-trip.</summary>
public class DiscoveryCursorTests
{
    [Fact]
    public void DiscoveryCursor_RoundTripsAllFourFieldsLossless()
    {
        var original = new DiscoveryCursor(
            LastActiveAt: new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero),
            TrustScore: 75,
            Id: new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
            IsOpenToday: true);

        var encoded = DiscoveryCursor.Encode(original);
        var decoded = DiscoveryCursor.TryDecode(encoded, out var result);

        decoded.Should().BeTrue();
        result.LastActiveAt.Should().Be(original.LastActiveAt);
        result.TrustScore.Should().Be(original.TrustScore);
        result.Id.Should().Be(original.Id);
        result.IsOpenToday.Should().Be(original.IsOpenToday);
    }

    [Fact]
    public void DiscoveryCursor_DecodingNonBase64_ReturnsFalse()
    {
        var result = DiscoveryCursor.TryDecode("not-valid-base64!!!", out _);
        result.Should().BeFalse();
    }

    [Fact]
    public void DiscoveryCursor_DecodingBase64ButWrongJsonShape_ReturnsFalse()
    {
        // Valid base64 but doesn't decode to the correct JSON shape
        var wrongJson = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"foo\":\"bar\"}"));
        var result = DiscoveryCursor.TryDecode(wrongJson, out _);
        result.Should().BeFalse();
    }

    [Fact]
    public void DiscoveryCursor_DecodingNull_ReturnsFalse()
    {
        var result = DiscoveryCursor.TryDecode(null, out _);
        result.Should().BeFalse();
    }

    [Fact]
    public void DiscoveryCursor_DecodingEmpty_ReturnsFalse()
    {
        var result = DiscoveryCursor.TryDecode(string.Empty, out _);
        result.Should().BeFalse();
    }

    [Fact]
    public void DiscoveryCursor_RoundTripsWithIsOpenTodayFalse()
    {
        var original = new DiscoveryCursor(
            LastActiveAt: new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.Zero),
            TrustScore: 0,
            Id: Guid.Empty,
            IsOpenToday: false);

        var encoded = DiscoveryCursor.Encode(original);
        DiscoveryCursor.TryDecode(encoded, out var result).Should().BeTrue();
        result.IsOpenToday.Should().BeFalse();
        result.TrustScore.Should().Be(0);
        result.Id.Should().Be(Guid.Empty);
    }
}
