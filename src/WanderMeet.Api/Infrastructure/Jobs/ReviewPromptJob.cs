using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Features.Meetups.Realtime;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.Infrastructure.Push;
using WanderMeet.Shared;

namespace WanderMeet.Api.Infrastructure.Jobs;

/// <summary>
/// Projection used by <see cref="ReviewPromptJob"/> to fetch all data needed for the FCM fan-out
/// in a single SQL round-trip (AsNoTracking).
/// </summary>
/// <param name="MeetupId">The meetup identifier.</param>
/// <param name="UserAId">Sender side of the meetup.</param>
/// <param name="UserBId">Receiver side of the meetup.</param>
/// <param name="UserAFcmToken">FCM registration token for user A; null means no push.</param>
/// <param name="UserAFirstName">Display first name of user A (used in UserB's push body).</param>
/// <param name="UserBFcmToken">FCM registration token for user B; null means no push.</param>
/// <param name="UserBFirstName">Display first name of user B (used in UserA's push body).</param>
internal sealed record ReviewPromptProjection(
    Guid MeetupId,
    Guid UserAId,
    Guid UserBId,
    string? UserAFcmToken,
    string UserAFirstName,
    string? UserBFcmToken,
    string UserBFirstName);

/// <summary>
/// Hangfire job that sends post-meetup review prompt push notifications to both participants
/// once the <see cref="ValidationConstants.ReviewPromptDelay"/> (3 hours) has elapsed since
/// <c>Meetup.MetAt</c>.
/// <para>
/// Read path uses <c>AsNoTracking</c> with a projection. The <c>PromptSent</c> flip is
/// performed via <c>ExecuteUpdateAsync</c> — a single SQL UPDATE, regardless of FCM outcome.
/// </para>
/// <para>
/// PII guard: log statements reference only <c>Meetup.Id</c> / <c>User.Id</c>.
/// Push bodies interpolate the OTHER participant's <c>FirstName</c> only — no FCM token,
/// email, or Bio is logged or pushed.
/// </para>
/// </summary>
internal sealed class ReviewPromptJob(
    WanderMeetDbContext dbContext,
    IFcmClient fcmClient,
    TimeProvider timeProvider,
    ILogger<ReviewPromptJob> logger)
{
    /// <summary>
    /// Processes up to 100 meetups eligible for a review prompt, fires FCM pushes to each
    /// participant (if they have a token), then bulk-flips <c>PromptSent=true</c>.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var threshold = now - ValidationConstants.ReviewPromptDelay;

        // Single AsNoTracking projection — joins UserA and UserB in one SQL round-trip.
        // threshold is computed in C# to avoid EF Core translation issues with static-readonly TimeSpan.
        var batch = await dbContext.Meetups
            .AsNoTracking()
            .Where(m => !m.PromptSent && m.MetAt <= threshold)
            .Take(100)
            .Select(m => new ReviewPromptProjection(
                m.Id,
                m.UserAId,
                m.UserBId,
                m.UserA!.FcmToken,
                m.UserA.FirstName,
                m.UserB!.FcmToken,
                m.UserB.FirstName))
            .ToListAsync(ct);

        if (batch.Count == 0)
        {
            logger.LogInformation("Job tick {Job} {Result}", "ReviewPrompt", "NoCandidates");
            return;
        }

        var batchIds = batch.Select(p => p.MeetupId).ToList();
        var successCount = 0;

        foreach (var row in batch)
        {
            // Push to UserA (body mentions UserB's name)
            if (!string.IsNullOrEmpty(row.UserAFcmToken))
            {
                var (title, body) = MeetupPushTemplates.ReviewPrompt(row.UserBFirstName);
                try
                {
                    await fcmClient.SendAsync(row.UserAFcmToken, title, body, ct);
                    successCount++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "FCM push failed {MeetupId} {UserId} {Phase}",
                        row.MeetupId, row.UserAId, "ReviewPrompt");
                }
            }

            // Push to UserB (body mentions UserA's name)
            if (!string.IsNullOrEmpty(row.UserBFcmToken))
            {
                var (title, body) = MeetupPushTemplates.ReviewPrompt(row.UserAFirstName);
                try
                {
                    await fcmClient.SendAsync(row.UserBFcmToken, title, body, ct);
                    successCount++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "FCM push failed {MeetupId} {UserId} {Phase}",
                        row.MeetupId, row.UserBId, "ReviewPrompt");
                }
            }
        }

        // Bulk-flip PromptSent=true regardless of FCM outcome (idempotency anchor)
        await dbContext.Meetups
            .Where(m => batchIds.Contains(m.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.PromptSent, true), ct);

        logger.LogInformation("Job tick {Job} {Result} {BatchSize} {PushCount}",
            "ReviewPrompt", "Completed", batch.Count, successCount);
    }
}
