using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;
using WanderMeet.Api.Features.Invites.Shared;
using WanderMeet.Api.Infrastructure.Push;

namespace WanderMeet.Api.IntegrationTests.Infrastructure;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> for integration tests.
/// Overrides the database connection string and replaces the JwtBearer signing-key
/// with an in-process RSA key so tests can issue their own tokens without Azure AD B2C.
/// </summary>
public sealed class WanderMeetApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _blobConnectionString;
    private readonly TestJwtTokenFactory _jwtFactory;

    /// <summary>
    /// The fake time provider registered in the test application. Initialised to real "now"
    /// so signed assets like Azure Blob SAS URIs validate against Azurite's wall-clock —
    /// the default <see cref="FakeTimeProvider"/> seed is 2000-01-01, which would produce
    /// a SAS that expired 25+ years ago.
    /// </summary>
    public FakeTimeProvider FakeTimeProvider { get; } = new(DateTimeOffset.UtcNow);

    /// <summary>
    /// The recording FCM client registered in the test application.
    /// Use in tests to assert push sends or inject failures via <see cref="RecordingFcmClient.ThrowOnSend"/>.
    /// Call <see cref="RecordingFcmClient.Reset"/> before each test (done automatically in <see cref="IntegrationTestBase.SetupAsync"/>).
    /// </summary>
    public RecordingFcmClient FcmClient { get; } = new();

    /// <summary>The JWT factory whose public key is configured into JwtBearer.</summary>
    public TestJwtTokenFactory JwtFactory => _jwtFactory;

    /// <summary>Initialises the factory with the Testcontainers connection strings.</summary>
    public WanderMeetApiFactory(string connectionString, string blobConnectionString, TestJwtTokenFactory jwtFactory)
    {
        _connectionString = connectionString;
        _blobConnectionString = blobConnectionString;
        _jwtFactory = jwtFactory;
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set the environment to "IntegrationTest" so Program.cs skips AddHangfireServer
        // (the Hangfire worker would poll the DB and introduce contention during tests).
        // AddHangfire (storage + IRecurringJobManager) is still registered so DI resolves.
        builder.UseEnvironment("IntegrationTest");

        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("BlobStorage:ConnectionString", _blobConnectionString);
        builder.UseSetting("BlobStorage:ContainerName", "user-photos-tests");
        // Azurite only listens on HTTP. Production pins SAS to HTTPS-only (security audit
        // finding F4); the test fixture overrides to exercise SAS uploads via the HTTP emulator.
        builder.UseSetting("BlobStorage:SasProtocol", "HttpsAndHttp");

        // Provide minimal AzureAdB2C config so the endpoint does not return 503 in integration tests.
        // Individual tests that need the real B2C call swap the handler via CreateClientWithB2CHandler.
        builder.UseSetting("AzureAdB2C:Instance", "https://login.microsoftonline.com");
        builder.UseSetting("AzureAdB2C:TenantId", "test-tenant-id");
        builder.UseSetting("AzureAdB2C:PolicyId", "B2C_1_signupsignin");
        builder.UseSetting("AzureAdB2C:ClientId", "test-client-id");

        builder.ConfigureServices(services =>
        {
            // Replace TimeProvider.System with FakeTimeProvider for deterministic assertions
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(FakeTimeProvider);

            // Replace IFcmClient with RecordingFcmClient for test assertions and failure injection
            services.RemoveAll<IFcmClient>();
            services.AddSingleton<IFcmClient>(FcmClient);
            services.AddSingleton(FcmClient);

            // Replace JwtBearer options: use in-process RSA key, no Authority (offline), RS256 only
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.MetadataAddress = null!;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = TestJwtTokenFactory.Issuer,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = _jwtFactory.SecurityKey,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ClockSkew = TimeSpan.FromMinutes(2),
                };
            });
        });
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> whose outbound requests to the <c>AzureAdB2C</c>
    /// named <see cref="System.Net.Http.HttpClient"/> are handled by <paramref name="b2cHandler"/>
    /// instead of the real HTTP stack.  All other application configuration (DB, JWT, time) is
    /// inherited from this factory.
    /// </summary>
    /// <param name="b2cHandler">The fake message handler to substitute for B2C calls.</param>
    public HttpClient CreateClientWithB2CHandler(HttpMessageHandler b2cHandler)
    {
        var derived = WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.PostConfigure<HttpClientFactoryOptions>("AzureAdB2C", opts =>
                    opts.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                        handlerBuilder.PrimaryHandler = b2cHandler))));

        return derived.CreateClient();
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> whose <see cref="IInviteNotifier"/> registration is
    /// replaced by <paramref name="notifier"/>. Use a <see cref="RecordingInviteNotifier"/> spy
    /// to assert notifier interactions or to inject failures (<c>ThrowOnSent</c> / <c>ThrowOnAccepted</c>).
    /// </summary>
    public HttpClient CreateClientWithInviteNotifier(IInviteNotifier notifier)
    {
        var derived = WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IInviteNotifier>();
                services.AddSingleton(notifier);
            }));

        return derived.CreateClient();
    }

    /// <summary>
    /// Creates an authenticated <see cref="HubConnection"/> for the given <paramref name="azureAdB2CId"/>
    /// pointed at the specified <paramref name="hubPath"/> against the test server.
    /// Uses <see cref="HttpTransportType.LongPolling"/> because <see cref="Microsoft.AspNetCore.TestHost.TestServer"/>
    /// does not support raw WebSockets.
    /// </summary>
    /// <param name="azureAdB2CId">The Azure AD B2C subject claim to use for the JWT bearer token.</param>
    /// <param name="hubPath">Hub endpoint path; defaults to <c>/hubs/invites</c>.</param>
    public HubConnection CreateAuthenticatedSignalRConnection(string azureAdB2CId, string hubPath = "/hubs/invites")
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, hubPath), opts =>
            {
                opts.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                opts.AccessTokenProvider = () => Task.FromResult<string?>(_jwtFactory.CreateToken(azureAdB2CId));
                opts.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }
}
