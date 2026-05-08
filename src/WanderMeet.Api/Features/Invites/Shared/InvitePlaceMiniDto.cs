using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Features.Invites.Shared;

/// <summary>Minimal place info embedded in invite responses.</summary>
/// <param name="Id">Place primary key.</param>
/// <param name="Name">Display name of the place.</param>
/// <param name="Category">Category of the place.</param>
public record InvitePlaceMiniDto(Guid Id, string Name, PlaceCategory Category);
