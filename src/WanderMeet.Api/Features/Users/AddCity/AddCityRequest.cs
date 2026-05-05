namespace WanderMeet.Api.Features.Users.AddCity;

/// <summary>Body for <c>POST /users/me/cities</c>.</summary>
/// <param name="CityId">Reference to the city the user is travelling to.</param>
/// <param name="ArrivedAt">UTC timestamp when the user arrived; must be ≤ now.</param>
public record AddCityRequest(Guid CityId, DateTimeOffset ArrivedAt);
