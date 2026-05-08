namespace WanderMeet.Api.Features.Invites.Shared;

/// <summary>
/// Response wrapper returned by all list-invite endpoints (incoming, sent, past).
/// Capped at 50 items — cursor pagination is deferred to a future UC.
/// </summary>
/// <param name="Items">Up to 50 invite DTOs ordered per the endpoint's sort contract.</param>
public record ListInvitesResponse(IReadOnlyList<InviteDto> Items);
