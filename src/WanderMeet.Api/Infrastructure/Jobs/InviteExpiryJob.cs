using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Invites.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Infrastructure.Jobs;

/// <summary>
/// Hangfire job that expires pending invites whose <c>ExpiresAt</c> has passed.
/// <para>
/// Mutating path: loads tracked entities (no <c>AsNoTracking</c>) so EF can detect changes.
/// </para>
/// <para>
/// PII guard: log statements reference only <c>InviteId</c> — no sender/receiver names or emails.
/// </para>
/// </summary>
internal sealed class InviteExpiryJob(
    WanderMeetDbContext dbContext,
    IInviteNotifier inviteNotifier,
    TimeProvider timeProvider,
    ILogger<InviteExpiryJob> logger)
{
    /// <summary>
    /// Expires up to 500 pending invites whose <c>ExpiresAt</c> is in the past, then fires
    /// per-invite notifications. Notifier failures are caught per-invite so one failure
    /// cannot prevent the rest from being notified.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();

        // Tracked load — we will mutate Status and RespondedAt
        var expired = await dbContext.Invites
            .Where(i => i.Status == InviteStatus.Pending && i.ExpiresAt <= now)
            .Take(500)
            .ToListAsync(ct);

        if (expired.Count == 0)
        {
            logger.LogInformation("Job tick {Job} {Result}", "InviteExpiry", "NoCandidates");
            return;
        }

        foreach (var invite in expired)
        {
            invite.Status = InviteStatus.Expired;
            invite.RespondedAt = now;
        }

        await dbContext.SaveChangesAsync(ct);

        // Notify after persist — failure here must NOT roll back persisted state
        foreach (var invite in expired)
        {
            try
            {
                await inviteNotifier.InviteExpiredAsync(invite, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Notifier failed {InviteId} {Phase}", invite.Id, "InviteExpired");
            }
        }

        logger.LogInformation("Job tick {Job} {Result} {Count}", "InviteExpiry", "Completed", expired.Count);
    }
}
