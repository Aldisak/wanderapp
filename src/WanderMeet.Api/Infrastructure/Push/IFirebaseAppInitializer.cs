namespace WanderMeet.Api.Infrastructure.Push;

/// <summary>
/// Strategy for bootstrapping the Firebase Admin SDK <c>FirebaseApp</c> singleton.
/// Extracted so unit tests can substitute a fake without invoking the real SDK
/// (which would touch the global <c>FirebaseApp</c> singleton state across tests).
/// </summary>
internal interface IFirebaseAppInitializer
{
    /// <summary>
    /// Creates the Firebase app from the supplied options. Throws on bad credentials.
    /// </summary>
    /// <param name="options">Firebase configuration options.</param>
    void Initialise(FirebaseOptions options);
}
