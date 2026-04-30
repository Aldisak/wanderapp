namespace WanderMeet.Shared;

/// <summary>String length and numeric range limits enforced by entity configurations and validators.</summary>
public static class ValidationConstants
{
    /// <summary>Max length of <c>User.Bio</c>.</summary>
    public const int BioMaxLength = 160;

    /// <summary>Max length of <c>User.FirstName</c>.</summary>
    public const int FirstNameMaxLength = 80;

    /// <summary>Max length of <c>MeetupReview.Text</c>.</summary>
    public const int ReviewTextMaxLength = 120;

    /// <summary>Max length of <c>Report.Reason</c>.</summary>
    public const int ReportReasonMaxLength = 300;

    /// <summary>Max number of photos a user can upload.</summary>
    public const int MaxPhotosPerUser = 4;

    /// <summary>Min trust score (clamp lower bound).</summary>
    public const int TrustScoreMin = 0;

    /// <summary>Max trust score (clamp upper bound).</summary>
    public const int TrustScoreMax = 100;

    /// <summary>Discovery radius around a city center, in metres.</summary>
    public const int DiscoveryRadiusMetres = 50_000;

    /// <summary>Default invite expiry — sent_at + 48 hours.</summary>
    public static readonly TimeSpan InviteExpiryWindow = TimeSpan.FromHours(48);

    /// <summary>Active-recently cutoff for the discovery feed (72 hours).</summary>
    public static readonly TimeSpan DiscoveryActiveWindow = TimeSpan.FromHours(72);

    /// <summary>SRID for all geography columns (WGS 84).</summary>
    public const int GeographySrid = 4326;
}
