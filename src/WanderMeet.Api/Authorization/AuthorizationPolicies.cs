namespace WanderMeet.Api.Authorization;

/// <summary>Names of all authorization policies registered in <c>Program.cs</c>.</summary>
public static class AuthorizationPolicies
{
    /// <summary>Default policy for all authenticated end-user endpoints.</summary>
    public const string UsersOnly = nameof(UsersOnly);

    /// <summary>Admin-only policy — gates the (future) Hangfire dashboard / moderation tools.</summary>
    public const string AdminOnly = nameof(AdminOnly);
}
