using FastEndpoints;
using Microsoft.AspNetCore.Http;
using WanderMeet.Api.Common;

namespace WanderMeet.Api.Features.Health.GetHealth;

/// <summary>Liveness probe — anonymous, used by Container Apps + uptime monitors.</summary>
internal sealed class GetHealthEndpoint(TimeProvider timeProvider) : EndpointWithoutRequest<GetHealthResponse>
{
    private readonly HealthFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Get("health");
        AllowAnonymous();
        Description(builder => builder
            .WithName(nameof(GetHealthEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();

        Summary(s =>
        {
            s.Summary = "Liveness probe";
            s.Description = "Returns 200 with a static payload while the process is alive. Anonymous on purpose — used by Azure Container Apps health probes.";
            s.Responses[StatusCodes.Status200OK] = "Service alive";
        });
    }

    /// <inheritdoc />
    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(new GetHealthResponse("healthy", timeProvider.GetUtcNow()), ct);
}
