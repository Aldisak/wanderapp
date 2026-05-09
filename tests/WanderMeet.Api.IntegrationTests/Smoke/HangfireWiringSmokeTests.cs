using FluentAssertions;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WanderMeet.Api.Infrastructure.Jobs;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Smoke;

/// <summary>
/// Smoke tests that verify the Hangfire wiring introduced in WI-1 boots cleanly in the
/// integration-test environment and that key Hangfire services resolve from the DI container.
/// </summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class HangfireWiringSmokeTests : IntegrationTestBase
{
    /// <summary>Initialises the test with the shared fixture.</summary>
    public HangfireWiringSmokeTests(IntegrationTestFixture app) : base(app)
    {
    }

    /// <summary>
    /// Verifies that <see cref="IRecurringJobManager"/> is registered and resolvable after the
    /// factory boots, and that <see cref="JobsStartupHostedService.StartAsync"/> completes without
    /// throwing when no job types are registered yet (WI-1 scaffold, no jobs).
    /// </summary>
    [Fact]
    public async Task WanderMeetApiFactory_BootsCleanly_WithHangfireRegistered()
    {
        // Arrange — factory is already booted by the fixture; just validate the DI output.
        var ct = TestContext.Current.CancellationToken;

        // Assert: IRecurringJobManager resolves from the application's DI container
        using var scope = App.Services.CreateScope();
        var recurringJobManager = scope.ServiceProvider.GetService<IRecurringJobManager>();
        recurringJobManager.Should().NotBeNull(because: "Hangfire registers IRecurringJobManager via AddHangfire");

        // Assert: JobsStartupHostedService resolves and StartAsync does not throw
        var hostedService = scope.ServiceProvider.GetService<JobsStartupHostedService>();
        // JobsStartupHostedService is registered via AddHostedService, which uses IHostedService.
        // Resolve as IHostedService implementations to find it.
        var allHostedServices = scope.ServiceProvider.GetServices<IHostedService>();
        var jobsService = allHostedServices.OfType<JobsStartupHostedService>().FirstOrDefault();
        jobsService.Should().NotBeNull(because: "JobsFeatureConfiguration registers JobsStartupHostedService via AddHostedService");

        // Act + Assert: StartAsync must not throw even with no job entries yet (WI-1 scaffold)
        var act = async () => await jobsService!.StartAsync(ct);
        await act.Should().NotThrowAsync(because: "WI-1 StartAsync only logs — no AddOrUpdate calls yet");
    }
}
