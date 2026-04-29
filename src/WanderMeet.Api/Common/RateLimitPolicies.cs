namespace WanderMeet.Api.Common;

/// <summary>Names of the five rate-limit policies registered in <c>Program.cs</c>.</summary>
public static class RateLimitPolicies
{
    /// <summary>100 req/min per IP — default for authenticated reads.</summary>
    public const string GeneralApi = nameof(GeneralApi);

    /// <summary>10 req/min per IP — brute-force prevention on register/refresh.</summary>
    public const string AuthEndpoints = nameof(AuthEndpoints);

    /// <summary>20 invites/hour per user — anti-spam on invite send.</summary>
    public const string InviteSend = nameof(InviteSend);

    /// <summary>60 req/min per user — discovery feed fetch.</summary>
    public const string Discovery = nameof(Discovery);

    /// <summary>5/day per user — report submission throttle.</summary>
    public const string Reports = nameof(Reports);
}
