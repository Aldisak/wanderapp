using FluentAssertions;
using WanderMeet.Api.Features.Places.Shared;
using WanderMeet.Shared.Enums;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Places.Shared;

/// <summary>Unit tests for <see cref="HangoutTagToPlaceCategory"/>.</summary>
public class HangoutTagToPlaceCategoryTests
{
    /// <summary>Coffee maps to Cafe.</summary>
    [Fact]
    public void TryMap_Coffee_MapsToCafe()
    {
        var result = HangoutTagToPlaceCategory.TryMap(HangoutTagSlug.Coffee, out var category);
        result.Should().BeTrue();
        category.Should().Be(PlaceCategory.Cafe);
    }

    /// <summary>Walk maps to Park.</summary>
    [Fact]
    public void TryMap_Walk_MapsToPark()
    {
        var result = HangoutTagToPlaceCategory.TryMap(HangoutTagSlug.Walk, out var category);
        result.Should().BeTrue();
        category.Should().Be(PlaceCategory.Park);
    }

    /// <summary>Food maps to Restaurant.</summary>
    [Fact]
    public void TryMap_Food_MapsToRestaurant()
    {
        var result = HangoutTagToPlaceCategory.TryMap(HangoutTagSlug.Food, out var category);
        result.Should().BeTrue();
        category.Should().Be(PlaceCategory.Restaurant);
    }

    /// <summary>Explore maps to Landmark.</summary>
    [Fact]
    public void TryMap_Explore_MapsToLandmark()
    {
        var result = HangoutTagToPlaceCategory.TryMap(HangoutTagSlug.Explore, out var category);
        result.Should().BeTrue();
        category.Should().Be(PlaceCategory.Landmark);
    }

    /// <summary>Cowork maps to Cowork.</summary>
    [Fact]
    public void TryMap_Cowork_MapsToCowork()
    {
        var result = HangoutTagToPlaceCategory.TryMap(HangoutTagSlug.Cowork, out var category);
        result.Should().BeTrue();
        category.Should().Be(PlaceCategory.Cowork);
    }
}
