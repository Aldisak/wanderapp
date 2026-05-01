using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using WanderMeet.Api.Common;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Auth.RefreshToken;

/// <summary>Proxies a refresh-token exchange to the Azure AD B2C token endpoint.</summary>
internal sealed class RefreshEndpoint(IHttpClientFactory httpClientFactory, IOptions<AzureAdB2COptions> options)
    : Endpoint<RefreshRequest, RefreshResponse>
{
    private readonly AuthFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Post("auth/refresh");
        AllowAnonymous();
        Description(b => b
            .WithName(nameof(RefreshEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.AuthEndpoints));
        DontCatchExceptions();

        Summary(s =>
        {
            s.Summary = "Exchange a refresh token for a new access + refresh token pair";
            s.Description = "Proxies the refresh token to Azure AD B2C and returns the new tokens. The refresh token is the credential — no Bearer header required.";
            s.Responses[StatusCodes.Status200OK] = "New access and refresh tokens";
            s.Responses[StatusCodes.Status400BadRequest] = "Validation error (RefreshToken missing)";
            s.Responses[StatusCodes.Status401Unauthorized] = "B2C rejected the refresh token";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
            s.Responses[StatusCodes.Status503ServiceUnavailable] = "Azure AD B2C is not configured";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(RefreshRequest req, CancellationToken ct)
    {
        var opts = options.Value;

        if (string.IsNullOrWhiteSpace(opts.Instance)
            || string.IsNullOrWhiteSpace(opts.TenantId)
            || string.IsNullOrWhiteSpace(opts.PolicyId))
        {
            AddError(ErrorCodes.Auth.B2CNotConfigured, "Azure AD B2C is not configured.");
            await Send.ErrorsAsync(503, ct);
            return;
        }

        var tokenUrl = $"{opts.Instance.TrimEnd('/')}/{opts.TenantId}/{opts.PolicyId}/oauth2/v2.0/token";

        var formContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("client_id", opts.ClientId ?? string.Empty),
            new KeyValuePair<string, string>("refresh_token", req.RefreshToken),
        ]);

        var client = httpClientFactory.CreateClient("AzureAdB2C");
        var b2cResponse = await client.PostAsync(tokenUrl, formContent, ct);

        if (!b2cResponse.IsSuccessStatusCode)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var tokenJson = await b2cResponse.Content.ReadAsStringAsync(ct);
        var tokenResult = JsonSerializer.Deserialize<B2CTokenResponse>(tokenJson);

        await Send.OkAsync(
            new RefreshResponse(tokenResult!.AccessToken, tokenResult.RefreshToken),
            ct);
    }

    /// <summary>Internal DTO for deserializing the B2C token endpoint response.</summary>
    private sealed class B2CTokenResponse
    {
        /// <summary>The issued access token.</summary>
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        /// <summary>The issued refresh token.</summary>
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; init; } = string.Empty;
    }
}
