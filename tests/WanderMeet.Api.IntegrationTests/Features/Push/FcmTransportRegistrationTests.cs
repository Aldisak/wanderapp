using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WanderMeet.Api.Infrastructure.Push;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Push;

/// <summary>
/// Integration tests verifying that <c>PushFeatureConfiguration</c> registers
/// the correct <see cref="IFcmClient"/> implementation based on the Firebase config.
/// </summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class FcmTransportRegistrationTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    /// <summary>
    /// When no Firebase credentials are configured (default test environment),
    /// <see cref="IFcmClient"/> should resolve to <see cref="NoOpFcmClient"/>.
    /// </summary>
    [Fact]
    public void PushFeatureConfiguration_NoFirebaseConfig_RegistersNoOpFcmClient()
    {
        // The default test factory does NOT set Firebase:CredentialsPath,
        // so PushFeatureConfiguration must fall back to NoOpFcmClient.
        using var scope = App.Services.CreateScope();
        var fcmClient = scope.ServiceProvider.GetRequiredService<IFcmClient>();

        fcmClient.Should().BeOfType<NoOpFcmClient>(
            because: "when Firebase:CredentialsPath is absent, IFcmClient must be NoOpFcmClient");
    }

    /// <summary>
    /// When Firebase credentials point to a non-existent file, <see cref="IFcmClient"/> must
    /// still resolve to <see cref="NoOpFcmClient"/>. Builds an isolated host whose configuration
    /// explicitly sets Firebase:CredentialsPath to a path that does not exist on disk; the
    /// File.Exists guard in PushFeatureConfiguration must reject it and select the NoOp client.
    /// </summary>
    [Fact]
    public void PushFeatureConfiguration_FirebaseCredentialsPathPointsToNonExistentFile_RegistersNoOpFcmClient()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");
        File.Exists(nonExistentPath).Should().BeFalse(because: "the test relies on this path NOT existing");

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Firebase:CredentialsPath"] = nonExistentPath,
                ["Firebase:ProjectId"] = "test-project"
            })
            .Build();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);

        var pushFeature = new PushFeatureConfigurationProbe();
        pushFeature.Register(services, configuration);

        using var provider = services.BuildServiceProvider();
        var fcmClient = provider.GetRequiredService<IFcmClient>();

        fcmClient.Should().BeOfType<NoOpFcmClient>(
            because: "Firebase:CredentialsPath points to a path that does not exist on disk → File.Exists is false → NoOp must win");
    }
}

/// <summary>
/// Probe that exposes <see cref="WanderMeet.Api.Features.Push.PushFeatureConfiguration"/>'s
/// AddFeatureDependencies for direct invocation in DI-registration tests.
/// </summary>
internal sealed class PushFeatureConfigurationProbe
{
    private readonly WanderMeet.Api.Features.Push.PushFeatureConfiguration _configuration = new();

    public void Register(IServiceCollection services, IConfiguration configuration)
        => _configuration.AddFeatureDependencies(services, configuration);
}
