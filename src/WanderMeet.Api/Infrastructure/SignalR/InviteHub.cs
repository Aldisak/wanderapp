using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WanderMeet.Api.Authorization;

namespace WanderMeet.Api.Infrastructure.SignalR;

/// <summary>
/// Server-push-only SignalR hub for invite lifecycle events.
/// Clients connect and listen; no client-callable methods are exposed.
/// Authentication is enforced via <see cref="AuthorizationPolicies.UsersOnly"/>.
/// </summary>
[Authorize(Policy = nameof(AuthorizationPolicies.UsersOnly))]
internal class InviteHub(ILogger<InviteHub> logger) : Hub
{
    /// <inheritdoc />
    public override Task OnConnectedAsync()
    {
        logger.LogDebug("SignalR InviteHub: client connected. ConnectionId={ConnectionId}, UserId={UserId}",
            Context.ConnectionId, Context.UserIdentifier);
        return base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogDebug("SignalR InviteHub: client disconnected. ConnectionId={ConnectionId}, UserId={UserId}",
            Context.ConnectionId, Context.UserIdentifier);
        return base.OnDisconnectedAsync(exception);
    }
}
