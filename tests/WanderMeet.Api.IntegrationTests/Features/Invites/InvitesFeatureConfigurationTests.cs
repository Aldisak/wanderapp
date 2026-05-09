using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WanderMeet.Api.Features.Invites.Realtime;
using WanderMeet.Api.Features.Invites.Shared;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Invites;

/// <summary>Integration tests verifying the Invites feature DI registration.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class InvitesFeatureConfigurationTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    /// <summary>IInviteNotifier should be registered as CompositeInviteNotifier after WI-2 swap.</summary>
    [Fact]
    public void Discover_FeatureConfiguration_RegistersIInviteNotifierAsCompositeInviteNotifier()
    {
        using var scope = App.Services.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<IInviteNotifier>();

        notifier.Should().NotBeNull();
        notifier.Should().BeOfType<CompositeInviteNotifier>();
    }
}
