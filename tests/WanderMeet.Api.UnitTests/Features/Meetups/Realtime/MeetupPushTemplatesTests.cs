using FluentAssertions;
using WanderMeet.Api.Features.Meetups.Realtime;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Meetups.Realtime;

/// <summary>Unit tests for <see cref="MeetupPushTemplates"/> message template formatting.</summary>
public class MeetupPushTemplatesTests
{
    /// <summary>ReviewPrompt returns title 'How did it go?'.</summary>
    [Fact]
    public void ReviewPrompt_TitleEqualsHowDidItGo()
    {
        var (title, _) = MeetupPushTemplates.ReviewPrompt("Bob");
        title.Should().Be("How did it go?");
    }

    /// <summary>ReviewPrompt body contains other participant's first name.</summary>
    [Fact]
    public void ReviewPrompt_BodyContainsOtherFirstName()
    {
        var (_, body) = MeetupPushTemplates.ReviewPrompt("Alice");
        body.Should().Be("You met Alice — let them know how it was.");
    }

    /// <summary>ReviewPrompt returns exactly correct title and body — positive equality test (PII guard).</summary>
    [Fact]
    public void ReviewPrompt_NoPiiLeak_TitleAndBodyEqualExpectedExactStrings()
    {
        const string otherFirstName = "Charlie";
        const string email = "charlie@example.com";
        const string fcmToken = "fcm-token-abc123";

        var (title, body) = MeetupPushTemplates.ReviewPrompt(otherFirstName);

        // Primary: positive equality
        title.Should().Be("How did it go?");
        body.Should().Be("You met Charlie — let them know how it was.");

        // Supplementary: PII must not appear
        title.Should().NotContain(email);
        title.Should().NotContain("@");
        title.Should().NotContain(fcmToken);
        body.Should().NotContain(email);
        body.Should().NotContain("@");
        body.Should().NotContain(fcmToken);
    }
}
