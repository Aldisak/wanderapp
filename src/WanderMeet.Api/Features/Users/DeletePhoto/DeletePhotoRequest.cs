namespace WanderMeet.Api.Features.Users.DeletePhoto;

/// <summary>Route-bound request for DELETE /api/v1/users/me/photos/{id}. No body validator required — the {id:guid} route constraint covers shape.</summary>
public record DeletePhotoRequest(Guid Id);
