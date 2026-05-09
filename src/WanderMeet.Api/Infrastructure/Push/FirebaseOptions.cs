namespace WanderMeet.Api.Infrastructure.Push;

/// <summary>
/// Configuration options for Firebase Admin SDK.
/// Bound from the <c>Firebase</c> configuration section.
/// </summary>
internal sealed class FirebaseOptions
{
    /// <summary>
    /// Absolute path to the Firebase service-account JSON credentials file.
    /// When <see langword="null" />, empty, or pointing to a non-existent file,
    /// <see cref="NoOpFcmClient"/> is registered instead.
    /// </summary>
    public string? CredentialsPath { get; set; }

    /// <summary>
    /// Firebase project identifier (e.g. <c>my-app-prod</c>).
    /// Used when initialising <c>FirebaseApp</c>.
    /// </summary>
    public string? ProjectId { get; set; }
}
