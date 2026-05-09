using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Infrastructure.EntityFramework;

namespace WanderMeet.Api.Infrastructure.SignalR;

/// <summary>
/// Maps a SignalR connection's JWT <c>sub</c> claim to the local <c>User.Id</c>.
/// Soft-deleted users return <c>null</c>, which causes SignalR to treat the connection as anonymous
/// and silently drop any <c>Clients.User(userId)</c> calls for that user-id.
/// </summary>
internal sealed class JwtSubUserIdProvider(IServiceScopeFactory scopeFactory) : IUserIdProvider
{
    /// <summary>
    /// Reads the JWT <c>sub</c> claim, queries the database for the matching non-deleted User row,
    /// and returns <c>User.Id.ToString()</c> — or <c>null</c> if the user is not found or soft-deleted.
    /// </summary>
    /// <param name="connection">The incoming hub connection context.</param>
    /// <returns>The local user id string, or <c>null</c> if unmapped.</returns>
    public string? GetUserId(HubConnectionContext connection)
    {
        var sub = connection.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sub))
        {
            return null;
        }

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();

        var userId = dbContext.Users
            .AsNoTracking()
            .Where(u => u.AzureAdB2CId == sub && u.DeletedAt == null)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefault();

        return userId?.ToString();
    }
}
