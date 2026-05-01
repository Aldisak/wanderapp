using System.Security.Claims;
using System.Threading.RateLimiting;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Common.Middleware;
using WanderMeet.Api.Infrastructure.EntityFramework;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddDbContext<WanderMeetDbContext>(options => options
    .UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.UseNetTopologySuite())
    .UseSnakeCaseNamingConvention());

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
            ValidateAudience = !string.IsNullOrWhiteSpace(audience),
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.UsersOnly, policy => policy
        .RequireAuthenticatedUser())
    .AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy
        .RequireAuthenticatedUser()
        .RequireRole("Admin"));

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
});

var app = builder.Build();

app.UseForwardedHeaders();

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
