using System.Security.Claims;
using System.Threading.RateLimiting;
using FastEndpoints;
using FastEndpoints.Swagger;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Common.Middleware;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.Infrastructure.Jobs;
using WanderMeet.Api.Infrastructure.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddDbContext<WanderMeetDbContext>(options => options
    .UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.UseNetTopologySuite())
    .UseSnakeCaseNamingConvention());

var hangfireConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddHangfire(c => c
    .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(hangfireConnectionString)));

// Skip the Hangfire server (worker) when running inside the integration-test host to avoid
// worker contention and to keep tests fast and deterministic.
if (!builder.Environment.IsEnvironment("IntegrationTest"))
{
    builder.Services.AddHangfireServer(opts =>
    {
        opts.WorkerCount = 1;
        opts.Queues = ["default"];
    });
}

builder.Services.AddCors(options =>
{
    var allowed = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowed)
        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
        .WithHeaders("Authorization", "Content-Type")
        .DisallowCredentials());
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = (context, ct) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return ValueTask.CompletedTask;
    };

    options.AddPolicy(RateLimitPolicies.GeneralApi, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: PartitionByIp(ctx),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy(RateLimitPolicies.AuthEndpoints, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: PartitionByIp(ctx),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy(RateLimitPolicies.InviteSend, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: PartitionByUserOrIp(ctx),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromHours(1)
            }));

    options.AddPolicy(RateLimitPolicies.Discovery, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: PartitionByUserOrIp(ctx),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy(RateLimitPolicies.Reports, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: PartitionByUserOrIp(ctx),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromDays(1)
            }));
});

// Fail-fast: in non-Development environments every AzureAdB2C config key MUST be present.
// Without this the API would silently accept tokens issued for any app in the tenant
// (security audit finding F2).
if (!builder.Environment.IsDevelopment())
{
    var b2cSection = builder.Configuration.GetSection("AzureAdB2C");
    string[] requiredKeys = ["Instance", "TenantId", "PolicyId", "ClientId"];
    var missing = requiredKeys
        .Where(k => string.IsNullOrWhiteSpace(b2cSection[k]))
        .ToArray();
    if (missing.Length > 0)
    {
        throw new InvalidOperationException(
            $"AzureAdB2C configuration is incomplete in environment '{builder.Environment.EnvironmentName}'. " +
            $"Missing keys: {string.Join(", ", missing)}. JWT validation cannot start safely.");
    }
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var b2c = builder.Configuration.GetSection("AzureAdB2C");
        var instance = b2c["Instance"];
        var tenantId = b2c["TenantId"];
        var policy = b2c["PolicyId"];
        var audience = b2c["ClientId"];

        if (!string.IsNullOrWhiteSpace(instance) &&
            !string.IsNullOrWhiteSpace(tenantId) &&
            !string.IsNullOrWhiteSpace(policy))
        {
            options.Authority = $"{instance}/{tenantId}/{policy}/v2.0/";
            options.Audience = audience;
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            // ValidateAudience is hard-coded true. Tests use PostConfigure<JwtBearerOptions>
            // to flip it false alongside the in-process signing key — production must never
            // skip the audience check (security audit finding F2).
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        // Accept access_token from query string for SignalR hub paths only.
        // Browser-based WebSocket clients cannot set Authorization headers.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    var accessToken = ctx.HttpContext.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        ctx.Token = accessToken;
                    }
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.UsersOnly, policy => policy
        .RequireAuthenticatedUser())
    .AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy
        .RequireAuthenticatedUser()
        .RequireRole("Admin"));

builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, JwtSubUserIdProvider>();

builder.Services.AddVerticalSliceFeatures<Program>(builder.Configuration);

builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument(o =>
{
    o.DocumentSettings = s =>
    {
        s.Title = "WanderMeet API";
        s.Version = "v1";
    };
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Trust exactly ONE upstream proxy hop. Without this, an attacker can chain
    // X-Forwarded-For values (e.g., `evil-ip, real-ip`) and the framework would
    // walk the chain past the legitimate proxy. Most container ingress setups
    // (Container Apps, Kubernetes Ingress, nginx-front-of-Kestrel) put exactly
    // one proxy in front of the app — so 1 is the correct default. Increase via
    // configuration if a multi-hop topology is in use.
    options.ForwardLimit = builder.Configuration.GetValue<int?>("ForwardedHeaders:ForwardLimit") ?? 1;

    // KnownNetworks: explicit list of CIDR ranges trusted to set X-Forwarded-For.
    // Defaults to loopback only — production must override via config to include
    // the actual ingress subnet (e.g., the Container Apps environment subnet).
    var knownNetworksRaw = builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>();
    if (knownNetworksRaw is { Length: > 0 })
    {
        options.KnownIPNetworks.Clear();
        foreach (var cidr in knownNetworksRaw)
        {
            // Format: "10.0.0.0/8" or "2001:db8::/32" — System.Net.IPNetwork.Parse handles both.
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(cidr));
        }
    }
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<InviteHub>("/hubs/invites");

app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireDashboardAuthorizationFilter()]
});

app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api/v1";
});

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerGen();
}

await app.RunAsync();

return;

static string PartitionByIp(HttpContext ctx)
    => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

static string PartitionByUserOrIp(HttpContext ctx)
{
    var sub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
    return string.IsNullOrEmpty(sub) ? PartitionByIp(ctx) : $"user:{sub}";
}

/// <summary>Visible to <c>WebApplicationFactory&lt;Program&gt;</c> in integration tests.</summary>
public partial class Program;
