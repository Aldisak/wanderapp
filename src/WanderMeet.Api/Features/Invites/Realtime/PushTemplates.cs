using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Features.Invites.Realtime;

/// <summary>
/// Factory for FCM push notification title/body pairs for invite lifecycle events.
/// All methods return a <c>(string Title, string Body)</c> tuple.
/// </summary>
internal static class PushTemplates
{
    /// <summary>
    /// Builds the standard "invite received" push notification for the receiver.
    /// Title is slug-specific; body is slug-independent.
    /// </summary>
    /// <param name="senderName">First name of the user who sent the invite.</param>
    /// <param name="placeName">Display name of the suggested place.</param>
    /// <param name="slug">Hangout type chosen at send time.</param>
    /// <returns>A <c>(Title, Body)</c> tuple ready to pass to <see cref="WanderMeet.Api.Infrastructure.Push.IFcmClient"/>.</returns>
    public static (string Title, string Body) Standard(string senderName, string placeName, HangoutTagSlug slug)
    {
        var displayName = slug switch
        {
            HangoutTagSlug.Coffee => "Coffee",
            HangoutTagSlug.Walk => "Walk",
            HangoutTagSlug.Food => "Food",
            HangoutTagSlug.Explore => "Explore",
            HangoutTagSlug.Cowork => "Cowork",
            _ => slug.ToString()
        };

        return (
            $"{displayName} at {placeName}?",
            $"{senderName} wants to meet you at {placeName}."
        );
    }

    /// <summary>
    /// Builds the "I'm already there" push notification for the receiver.
    /// Title always ends with ☕ (U+2615) regardless of slug; slug is kept on the signature
    /// for future per-slug emoji variations.
    /// </summary>
    /// <param name="senderName">First name of the user who is already at the place.</param>
    /// <param name="placeName">Display name of the place.</param>
    /// <param name="slug">Hangout type (currently unused in the output; reserved for future iterations).</param>
    /// <returns>A <c>(Title, Body)</c> tuple ready to pass to <see cref="WanderMeet.Api.Infrastructure.Push.IFcmClient"/>.</returns>
    public static (string Title, string Body) ImThere(string senderName, string placeName, HangoutTagSlug slug)
        => (
            $"{senderName} is at {placeName} ☕",
            "They’re there right now and would love some company."
        );

    /// <summary>
    /// Builds the "invite accepted" push notification for the original sender.
    /// </summary>
    /// <param name="receiverName">First name of the user who accepted the invite.</param>
    /// <param name="placeName">Display name of the place they are heading to.</param>
    /// <returns>A <c>(Title, Body)</c> tuple ready to pass to <see cref="WanderMeet.Api.Infrastructure.Push.IFcmClient"/>.</returns>
    public static (string Title, string Body) Accepted(string receiverName, string placeName)
        => (
            "See you there!",
            $"{receiverName} accepted — they’re on their way to {placeName}."
        );
}
