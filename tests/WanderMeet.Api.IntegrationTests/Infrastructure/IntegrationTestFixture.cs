using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Respawn;
using System.Net.Http.Headers;
using Testcontainers.Azurite;
using Testcontainers.PostgreSql;
using WanderMeet.Api.Features.Invites.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Async-lifetime fixture that owns the PostgreSQL and Azurite Testcontainers, applies EF migrations once,
/// and exposes helpers for resetting state between tests.
/// Shared across all tests in the <see cref="TestConstants.Collections.PipelineTest"/> collection.
/// </summary>
public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgis/postgis:17-3.5")
        .Build();

    private readonly AzuriteContainer _azurite = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.35.0")
        .Build();

    private Respawner _respawner = null!;
    private WanderMeetApiFactory _factory = null!;
    private readonly TestJwtTokenFactory _jwtFactory = new();

    /// <summary>The service provider from the running test application.</summary>
    public IServiceProvider Services => _factory.Services;

    /// <summary>The <see cref="FakeTimeProvider"/> registered in the test application; used for deterministic time assertions.</summary>
    public FakeTimeProvider FakeTimeProvider => _factory.FakeTimeProvider;

    /// <summary>The Azurite blob storage connection string for direct SDK calls in tests.</summary>
    public string BlobConnectionString => _azurite.GetConnectionString();

    /// <summary>Initialises the containers, runs EF migrations, and configures Respawn.</summary>
    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _azurite.StartAsync());

        _factory = new WanderMeetApiFactory(_postgres.GetConnectionString(), _azurite.GetConnectionString(), _jwtFactory);

        // Apply EF Core migrations via the test factory's service provider
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        await dbContext.Database.MigrateAsync();

        // Configure Respawn to reset between tests (uses Npgsql connection)
        await using var conn = new Npgsql.NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = [new Respawn.Graph.Table("spatial_ref_sys")],
        });
    }

    /// <summary>Resets all user data between tests. Must be the first call in <c>SetupAsync</c>.</summary>
    public async Task ResetDatabaseAsync()
    {
        await using var conn = new Npgsql.NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    /// <summary>Creates an anonymous HTTP client (no Authorization header).</summary>
    public HttpClient CreateAnonymousClient()
        => _factory.CreateClient();

    /// <summary>
    /// Creates an HTTP client where outbound calls to the <c>AzureAdB2C</c> named client
    /// are handled by <paramref name="b2cHandler"/> instead of the real network.
    /// </summary>
    public HttpClient CreateClientWithB2CHandler(HttpMessageHandler b2cHandler)
        => _factory.CreateClientWithB2CHandler(b2cHandler);

    /// <summary>
    /// Creates an HTTP client whose <see cref="IInviteNotifier"/> is replaced with the supplied
    /// instance — typically a <see cref="RecordingInviteNotifier"/> for asserting interactions
    /// or injecting failures.
    /// </summary>
    public HttpClient CreateClientWithInviteNotifier(IInviteNotifier notifier)
        => _factory.CreateClientWithInviteNotifier(notifier);

    /// <summary>
    /// Creates an HTTP client with a valid Bearer token whose <c>sub</c> claim equals <paramref name="azureAdB2CId"/>.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string azureAdB2CId)
    {
        var client = _factory.CreateClient();
        var token = _jwtFactory.CreateToken(azureAdB2CId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Creates an authenticated HTTP client (Bearer for <paramref name="azureAdB2CId"/>)
    /// whose <see cref="IInviteNotifier"/> is replaced with <paramref name="notifier"/>.
    /// </summary>
    public HttpClient CreateAuthenticatedClientWithInviteNotifier(string azureAdB2CId, IInviteNotifier notifier)
    {
        var client = _factory.CreateClientWithInviteNotifier(notifier);
        var token = _jwtFactory.CreateToken(azureAdB2CId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Stops the PostgreSQL and Azurite containers.</summary>
    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
        await _azurite.DisposeAsync();
    }
}
