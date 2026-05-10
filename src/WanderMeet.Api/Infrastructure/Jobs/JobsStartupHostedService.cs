using Hangfire;

namespace WanderMeet.Api.Infrastructure.Jobs;

/// <summary>
/// Hosted service that registers all recurring Hangfire jobs on application startup.
/// WI-1 ships the scaffold only — job <c>AddOrUpdate</c> calls are wired in WI-2.
/// </summary>
internal sealed class JobsStartupHostedService(
    IRecurringJobManager recurringJobs,
    ILogger<JobsStartupHostedService> logger) : IHostedService
{
    /// <summary>
    /// Registers recurring jobs with the Hangfire scheduler.
    /// Runs once on host startup; registrations are idempotent across restarts.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Recurring jobs registering");

        recurringJobs.AddOrUpdate<InviteExpiryJob>(
            "invite-expiry",
            j => j.ExecuteAsync(CancellationToken.None),
            "*/5 * * * *");

        recurringJobs.AddOrUpdate<ReviewPromptJob>(
            "review-prompt",
            j => j.ExecuteAsync(CancellationToken.None),
            "*/5 * * * *");

        recurringJobs.AddOrUpdate<SinkInactiveProfilesJob>(
            "sink-inactive-profiles",
            j => j.ExecuteAsync(CancellationToken.None),
            "0 * * * *");

        return Task.CompletedTask;
    }

    /// <summary>Nothing to clean up; Hangfire manages its own worker lifecycle.</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
