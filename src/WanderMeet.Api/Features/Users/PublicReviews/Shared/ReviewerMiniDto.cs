namespace WanderMeet.Api.Features.Users.PublicReviews.Shared;

/// <summary>Minimal reviewer info projected into each public review item.</summary>
public record ReviewerMiniDto(Guid Id, string FirstName, string? PhotoUrl);
