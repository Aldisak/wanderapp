namespace WanderMeet.Shared;

/// <summary>
/// Stable error code constants — used as frontend localization keys.
/// Format: <c>{Domain}.{ErrorName}</c>. Add per-feature nested classes as features land.
/// </summary>
public static class ErrorCodes
{
    /// <summary>Validation failures originating from FluentValidation rules.</summary>
    public static class Validation
    {
        /// <summary>User's first name was missing or whitespace-only.</summary>
        public const string FirstNameRequired = "Validation.FirstNameRequired";

        /// <summary>User's first name exceeded the maximum allowed length.</summary>
        public const string FirstNameTooLong = "Validation.FirstNameTooLong";

        /// <summary>Refresh token was missing or whitespace-only.</summary>
        public const string RefreshTokenRequired = "Validation.RefreshTokenRequired";
    }

    /// <summary>Auth-domain error codes.</summary>
    public static class Auth
    {
        /// <summary>An account with this Azure AD B2C identity already exists.</summary>
        public const string AlreadyRegistered = "Auth.AlreadyRegistered";

        /// <summary>The Azure AD B2C configuration section is missing required keys.</summary>
        public const string B2CNotConfigured = "Auth.B2CNotConfigured";
    }
}
