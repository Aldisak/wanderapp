namespace WanderMeet.Api.Features.Users.UploadPhoto;

/// <summary>Request body for POST /api/v1/users/me/photos.</summary>
/// <param name="Order">The display order slot (0 to MaxPhotosPerUser - 1).</param>
public record UploadPhotoRequest(int Order);
