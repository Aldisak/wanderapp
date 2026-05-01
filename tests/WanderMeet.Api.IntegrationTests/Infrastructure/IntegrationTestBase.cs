using FastEndpoints.Testing;

namespace WanderMeet.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for all integration tests.
/// Derives from <see cref="TestBase"/> (FastEndpoints.Testing) and resets the database
/// as the first step of every test setup.
/// </summary>
public abstract class IntegrationTestBase(IntegrationTestFixture app) : TestBase
{
    /// <summary>Shared fixture (PostgreSQL container, migrations, time provider, HTTP client factories).</summary>
    protected IntegrationTestFixture App { get; } = app;

    /// <inheritdoc />
    protected override async ValueTask SetupAsync()
    {
        await App.ResetDatabaseAsync();
    }
}
