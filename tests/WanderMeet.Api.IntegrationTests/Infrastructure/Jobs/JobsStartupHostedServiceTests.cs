using FluentAssertions;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Infrastructure.Jobs;

/// <summary>
/// Asserts that <c>JobsStartupHostedService</c> registered the three recurring jobs
/// with the documented cron schedules. Catches typo regressions in the cron strings.
/// </summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class JobsStartupHostedServiceTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    [Fact]
    public void Recurring_Jobs_Registered_With_Expected_Crons()
    {
        // The fixture invokes JobsStartupHostedService.StartAsync at host start, which
        // calls IRecurringJobManager.AddOrUpdate for each job.
        var connection = App.Services.GetRequiredService<JobStorage>().GetConnection();

        var inviteExpiry = connection.GetRecurringJobs(["invite-expiry"]).SingleOrDefault();
        var reviewPrompt = connection.GetRecurringJobs(["review-prompt"]).SingleOrDefault();
        var sinkInactive = connection.GetRecurringJobs(["sink-inactive-profiles"]).SingleOrDefault();

        inviteExpiry.Should().NotBeNull(because: "InviteExpiryJob recurring registration must exist");
        inviteExpiry!.Cron.Should().Be("*/5 * * * *");

        reviewPrompt.Should().NotBeNull(because: "ReviewPromptJob recurring registration must exist");
        reviewPrompt!.Cron.Should().Be("*/5 * * * *");

        sinkInactive.Should().NotBeNull(because: "SinkInactiveProfilesJob recurring registration must exist");
        sinkInactive!.Cron.Should().Be("0 * * * *");
    }
}
