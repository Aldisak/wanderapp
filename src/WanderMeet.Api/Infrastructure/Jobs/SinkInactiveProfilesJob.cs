using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Infrastructure.EntityFramework;

namespace WanderMeet.Api.Infrastructure.Jobs;

/// <summary>
/// Hangfire job that resets <c>User.IsOpenToday</c> to <c>false</c> for users who have not
/// been active in the last 24 hours.
/// <para>
/// Uses a single <c>ExecuteUpdateAsync</c> — no read round-trip. Zero rows updated when all
/// users are either already closed or have recent activity.
/// </para>
/// </summary>
internal sealed class SinkInactiveProfilesJob(
    WanderMeetDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<SinkInactiveProfilesJob> logger)
{
    /// <summary>
    /// Flips <c>IsOpenToday=false</c> for every user who has <c>IsOpenToday=true</c> and
    /// <c>LastActiveAt</c> older than 24 hours ago.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var threshold = timeProvider.GetUtcNow() - TimeSpan.FromHours(24);

        var affected = await dbContext.Users
            .Where(u => u.IsOpenToday && u.LastActiveAt < threshold)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsOpenToday, false), ct);

        logger.LogInformation("Job tick {Job} {Result} {Count}",
            "SinkInactiveProfiles", "Completed", affected);
    }
}
