namespace WanderMeet.Api.Infrastructure.Push;

/// <summary>
/// Abstraction over the FCM transport layer.
/// Implementations: <c>FirebaseAdminFcmClient</c> (production)
/// and <see cref="NoOpFcmClient"/> (development / test when credentials are absent).
/// </summary>
internal interface IFcmClient
{
    /// <summary>
    /// Sends a push notification to the device identified by <paramref name="fcmToken"/>.
    /// </summary>
    /// <param name="fcmToken">The FCM registration token for the target device.</param>
    /// <param name="title">Notification title.</param>
    /// <param name="body">Notification body text.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendAsync(string fcmToken, string title, string body, CancellationToken ct);
}
