namespace WanderMeet.Shared.Enums;

/// <summary>Lifecycle states for an invite.</summary>
public enum InviteStatus
{
    /// <summary>Sent and awaiting receiver action.</summary>
    Pending,
    /// <summary>Receiver accepted; a meetup record is created.</summary>
    Accepted,
    /// <summary>Receiver declined. Sender is intentionally not notified.</summary>
    Declined,
    /// <summary>Auto-expired by background job after 48 h without response.</summary>
    Expired
}
