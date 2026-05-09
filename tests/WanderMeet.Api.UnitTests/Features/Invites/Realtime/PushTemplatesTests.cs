using FluentAssertions;
using WanderMeet.Api.Features.Invites.Realtime;
using WanderMeet.Shared.Enums;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Invites.Realtime;

/// <summary>Unit tests for <see cref="PushTemplates"/> message template formatting.</summary>
public class PushTemplatesTests
{
    // -----------------------------------------------------------------------
    // Standard templates per slug
    // -----------------------------------------------------------------------

    /// <summary>Coffee slug produces correct title.</summary>
    [Fact]
    public void PushTemplates_StandardCoffee_TitleEqualsCoffeeAtPlaceQuestionMark()
    {
        var (title, _) = PushTemplates.Standard("Alice", "Cafe Lou", HangoutTagSlug.Coffee);
        title.Should().Be("Coffee at Cafe Lou?");
    }

    /// <summary>Walk slug produces correct title.</summary>
    [Fact]
    public void PushTemplates_StandardWalk_TitleEqualsWalkAtPlaceQuestionMark()
    {
        var (title, _) = PushTemplates.Standard("Alice", "Park View", HangoutTagSlug.Walk);
        title.Should().Be("Walk at Park View?");
    }

    /// <summary>Food slug produces correct title.</summary>
    [Fact]
    public void PushTemplates_StandardFood_TitleEqualsFoodAtPlaceQuestionMark()
    {
        var (title, _) = PushTemplates.Standard("Alice", "Bistro", HangoutTagSlug.Food);
        title.Should().Be("Food at Bistro?");
    }

    /// <summary>Explore slug produces correct title.</summary>
    [Fact]
    public void PushTemplates_StandardExplore_TitleEqualsExploreAtPlaceQuestionMark()
    {
        var (title, _) = PushTemplates.Standard("Alice", "Old Town", HangoutTagSlug.Explore);
        title.Should().Be("Explore at Old Town?");
    }

    /// <summary>Cowork slug produces correct title.</summary>
    [Fact]
    public void PushTemplates_StandardCowork_TitleEqualsCoworkAtPlaceQuestionMark()
    {
        var (title, _) = PushTemplates.Standard("Alice", "Hub Space", HangoutTagSlug.Cowork);
        title.Should().Be("Cowork at Hub Space?");
    }

    /// <summary>Body is slug-independent: always '{senderName} wants to meet you at {placeName}.'</summary>
    [Theory]
    [InlineData(HangoutTagSlug.Coffee)]
    [InlineData(HangoutTagSlug.Walk)]
    [InlineData(HangoutTagSlug.Food)]
    [InlineData(HangoutTagSlug.Explore)]
    [InlineData(HangoutTagSlug.Cowork)]
    public void PushTemplates_StandardAllSlugs_BodyEqualsSenderWantsToMeetYouAtPlace(HangoutTagSlug slug)
    {
        var (_, body) = PushTemplates.Standard("Alice", "Cafe Lou", slug);
        body.Should().Be("Alice wants to meet you at Cafe Lou.");
    }

    // -----------------------------------------------------------------------
    // PII non-leak: positive equality assertions
    // -----------------------------------------------------------------------

    /// <summary>
    /// PII test: positive equality per fixture, with supplementary substring-negative checks.
    /// Catches any future template change that interpolates email/oid/Bio into Title or Body.
    /// </summary>
    [Fact]
    public void PushTemplates_NoPiiLeak_TitleAndBodyEqualExpectedExactStrings()
    {
        const string senderName = "Alice";
        const string placeName = "Cafe Lou";
        const string email = "alice@example.com";
        const string oid = "oid|00000000-0000-0000-0000-alice00000001";
        const string bio = "Bio: nomad since 2019";

        var (title, body) = PushTemplates.Standard(senderName, placeName, HangoutTagSlug.Coffee);

        // Primary: positive equality
        title.Should().Be("Coffee at Cafe Lou?");
        body.Should().Be("Alice wants to meet you at Cafe Lou.");

        // Supplementary: PII substrings must not appear
        title.Should().NotContain(email);
        title.Should().NotContain("@");
        title.Should().NotContain(oid);
        title.Should().NotContain("oid|");
        title.Should().NotContain(bio);
        body.Should().NotContain(email);
        body.Should().NotContain("@");
        body.Should().NotContain(oid);
        body.Should().NotContain("oid|");
        body.Should().NotContain(bio);
    }

    // -----------------------------------------------------------------------
    // ImThere template
    // -----------------------------------------------------------------------

    /// <summary>ImThere title includes sender name, place name, and ☕ emoji.</summary>
    [Fact]
    public void PushTemplates_ImThere_TitleIncludesSenderNameAndCoffeeEmoji()
    {
        var (title, body) = PushTemplates.ImThere("Bob", "Cafe Blue", HangoutTagSlug.Coffee);
        title.Should().Be("Bob is at Cafe Blue ☕");
        body.Should().Be("They’re there right now and would love some company.");
    }

    // -----------------------------------------------------------------------
    // Accepted template
    // -----------------------------------------------------------------------

    /// <summary>Accepted title is 'See you there!' and body interpolates receiver name and place.</summary>
    [Fact]
    public void PushTemplates_Accepted_TitleAndBodyCorrect()
    {
        var (title, body) = PushTemplates.Accepted("Carol", "The Hub");
        title.Should().Be("See you there!");
        body.Should().Be("Carol accepted — they’re on their way to The Hub.");
    }
}
