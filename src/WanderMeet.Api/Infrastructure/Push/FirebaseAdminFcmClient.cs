using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WanderMeet.Api.Infrastructure.Push;

/// <summary>
/// Production implementation of <see cref="IFcmClient"/> using the Firebase Admin SDK.
/// Firebase app initialisation is lazy and runs on the first <see cref="SendAsync"/> call.
/// If initialisation fails, the failure is permanent for the process lifetime (no retry storm) —
/// subsequent calls log a Warning and return without re-attempting init.
/// </summary>
internal sealed class FirebaseAdminFcmClient(
    IOptions<FirebaseOptions> options,
    IFirebaseAppInitializer initializer,
    ILogger<FirebaseAdminFcmClient> logger) : IFcmClient
{
    private readonly System.Threading.Lock _lock = new();
    private bool _initAttempted;
    private bool _initFailed;

    /// <inheritdoc />
    public async Task SendAsync(string fcmToken, string title, string body, CancellationToken ct)
    {
        if (!EnsureInitialised())
        {
            logger.LogWarning("FCM message dropped {Reason}", "InitFailed");
            return;
        }

        var message = new Message
        {
            Token = fcmToken,
            Notification = new Notification { Title = title, Body = body }
        };

        try
        {
            await FirebaseMessaging.DefaultInstance.SendAsync(message, ct);
        }
        catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode == MessagingErrorCode.Unregistered)
        {
            logger.LogWarning("FCM message dropped {Reason}", "TokenUnregistered");
        }
        // All other FirebaseMessagingException values propagate; the composite's per-child catch records the failure.
    }

    private bool EnsureInitialised()
    {
        if (_initAttempted)
        {
            return !_initFailed;
        }

        lock (_lock)
        {
            if (_initAttempted)
            {
                return !_initFailed;
            }

            // Flip the once-flag BEFORE invoking the initializer so any concurrent call observes
            // _initAttempted == true and short-circuits without retrying on failure.
            _initAttempted = true;

            try
            {
                initializer.Initialise(options.Value);
            }
            catch (Exception ex)
            {
                _initFailed = true;
                logger.LogError(ex, "FCM init failed; push disabled for process lifetime");
                return false;
            }
        }

        return true;
    }
}
