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

        /// <summary>CityId query parameter is empty.</summary>
        public const string CityIdRequired = "Validation.CityIdRequired";

        /// <summary>Limit query parameter is outside the allowed 1–50 range.</summary>
        public const string LimitOutOfRange = "Validation.LimitOutOfRange";

        /// <summary>HangoutTagSlug query parameter is not a recognised enum value.</summary>
        public const string HangoutTagSlugInvalid = "Validation.HangoutTagSlugInvalid";

        /// <summary>Cursor query parameter is not a valid base64-encoded cursor.</summary>
        public const string CursorMalformed = "Validation.CursorMalformed";

        /// <summary>User's first name exceeded the maximum allowed length.</summary>
        public const string FirstNameTooLong = "Validation.FirstNameTooLong";

        /// <summary>Refresh token was missing or whitespace-only.</summary>
        public const string RefreshTokenRequired = "Validation.RefreshTokenRequired";

        /// <summary>The photo order slot is out of the valid range (0 to MaxPhotosPerUser-1).</summary>
        public const string PhotoOrderOutOfRange = "Validation.PhotoOrderOutOfRange";

        /// <summary>The user has already reached the maximum number of photos.</summary>
        public const string PhotoLimitReached = "Validation.PhotoLimitReached";

        /// <summary>The requested photo order slot is already occupied by an active photo.</summary>
        public const string PhotoOrderTaken = "Validation.PhotoOrderTaken";
    }

    /// <summary>Auth-domain error codes.</summary>
    public static class Auth
    {
        /// <summary>An account with this Azure AD B2C identity already exists.</summary>
        public const string AlreadyRegistered = "Auth.AlreadyRegistered";

        /// <summary>The Azure AD B2C configuration section is missing required keys.</summary>
        public const string B2CNotConfigured = "Auth.B2CNotConfigured";
    }

    /// <summary>User-domain error codes.</summary>
    public static class User
    {
        /// <summary>No local user profile exists for the authenticated identity.</summary>
        public const string NotRegistered = "User.NotRegistered";
    }

    /// <summary>Storage-domain error codes.</summary>
    public static class Storage
    {
        /// <summary>Blob storage is not configured (connection string is missing or empty).</summary>
        public const string NotConfigured = "Storage.NotConfigured";
    }

    /// <summary>UserPhoto-domain error codes.</summary>
    public static class UserPhoto
    {
        /// <summary>The requested photo was not found or is not owned by the caller.</summary>
        public const string NotFound = "UserPhoto.NotFound";
    }

    /// <summary>Additional validation error codes for user-profile operations.</summary>
    public static class UserValidation
    {
        /// <summary>User's bio exceeded the maximum allowed length.</summary>
        public const string BioTooLong = "Validation.BioTooLong";

        /// <summary>More than 5 hangout tag IDs were supplied.</summary>
        public const string HangoutTagIdsTooMany = "Validation.HangoutTagIdsTooMany";

        /// <summary>Duplicate hangout tag IDs were supplied.</summary>
        public const string HangoutTagIdsDuplicate = "Validation.HangoutTagIdsDuplicate";

        /// <summary>One or more supplied hangout tag IDs do not exist.</summary>
        public const string HangoutTagIdNotFound = "Validation.HangoutTagIdNotFound";

        /// <summary>City id was empty or refers to a non-existent city.</summary>
        public const string CityIdNotFound = "Validation.CityIdNotFound";

        /// <summary>Arrived-at timestamp was in the future.</summary>
        public const string ArrivedAtInFuture = "Validation.ArrivedAtInFuture";

        /// <summary>Departed-at timestamp was earlier than arrived-at.</summary>
        public const string DepartedAtBeforeArrived = "Validation.DepartedAtBeforeArrived";

        /// <summary>Departed-at timestamp was in the future.</summary>
        public const string DepartedAtInFuture = "Validation.DepartedAtInFuture";
    }

    /// <summary>UserCity-domain error codes.</summary>
    public static class UserCity
    {
        /// <summary>Travel-history record was not found for this user.</summary>
        public const string NotFound = "UserCity.NotFound";
    }

    /// <summary>Discovery-domain error codes.</summary>
    public static class Discovery
    {
        /// <summary>The requested city was not found or is soft-deleted.</summary>
        public const string CityNotFound = "Discovery.CityNotFound";
    }
}
