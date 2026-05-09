namespace WanderMeet.Api.Features.Meetups.Realtime;

/// <summary>
/// Factory for FCM push notification title/body pairs for meetup lifecycle events.
/// All methods return a <c>(string Title, string Body)</c> tuple.
/// PII guard: push bodies MUST NOT include email, FCM token, or Bio — only FirstName.
/// </summary>
internal static class MeetupPushTemplates
{
    /// <summary>
    /// Builds the 3-hour post-meetup review prompt push notification.
    /// Sent to each participant; <paramref name="otherFirstName"/> is the OTHER participant's name.
    /// </summary>
    /// <param name="otherFirstName">First name of the participant who is NOT receiving this push.</param>
    /// <returns>A <c>(Title, Body)</c> tuple ready to pass to <see cref="WanderMeet.Api.Infrastructure.Push.IFcmClient"/>.</returns>
    public static (string Title, string Body) ReviewPrompt(string otherFirstName)
        => (
            "How did it go?",
            $"You met {otherFirstName} — let them know how it was."
        );
}
