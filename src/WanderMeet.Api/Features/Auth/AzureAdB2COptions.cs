namespace WanderMeet.Api.Features.Auth;

/// <summary>Configuration values for the Azure AD B2C tenant used by this application.</summary>
internal sealed class AzureAdB2COptions
{
    /// <summary>The B2C login instance base URL (e.g. https://login.microsoftonline.com).</summary>
    public string? Instance { get; init; }

    /// <summary>The Azure AD B2C tenant identifier or domain name.</summary>
    public string? TenantId { get; init; }

    /// <summary>The user-flow / policy name (e.g. B2C_1_signupsignin).</summary>
    public string? PolicyId { get; init; }

    /// <summary>The application (client) ID registered in Azure AD B2C.</summary>
    public string? ClientId { get; init; }
}
