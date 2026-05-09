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
        logger.LogInformation("JobsStartupHostedService: registering recurring jobs (WI-2 will add job entries).");
        // AddOrUpdate calls for InviteExpiryJob, ReviewPromptJob, and SinkInactiveProfilesJob
        // are wired in WI-2. The recurringJobs manager is injected here to validate DI wiring
        // in integration smoke tests (WI-1).
        _ = recurringJobs;
        return Task.CompletedTask;
    }

    /// <summary>Nothing to clean up; Hangfire manages its own worker lifecycle.</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
