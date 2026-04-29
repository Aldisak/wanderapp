namespace WanderMeet.Api.Common;

/// <summary>Accessor for the authenticated user on the current request.</summary>
public interface ICurrentUser
{
    /// <summary>Database user id of the caller, or <c>null</c> if anonymous.</summary>
    Guid? UserId { get; }

    /// <summary>True when a valid JWT was presented and the user record exists.</summary>
    bool IsAuthenticated { get; }
}
