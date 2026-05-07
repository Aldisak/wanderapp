using WanderMeet.Api.Features.Users.Shared;

namespace WanderMeet.Api.Features.Discovery.Arriving;

/// <summary>A user who will soon arrive in the requested city.</summary>
public record ArrivingUserDto(PublicUserDto User, DateTimeOffset ArrivingAt);
