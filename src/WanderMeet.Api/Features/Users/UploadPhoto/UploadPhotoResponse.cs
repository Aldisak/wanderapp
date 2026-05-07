namespace WanderMeet.Api.Features.Users.UploadPhoto;

/// <summary>Response body for POST /api/v1/users/me/photos.</summary>
/// <param name="PhotoId">The newly created photo's unique identifier.</param>
/// <param name="BlobUrl">The canonical blob URL (without SAS query parameters).</param>
/// <param name="SasUrl">The write-only SAS URI scoped to this single blob path.</param>
/// <param name="SasExpiresAt">The UTC expiry time of the SAS token.</param>
public record UploadPhotoResponse(Guid PhotoId, string BlobUrl, string SasUrl, DateTimeOffset SasExpiresAt);
