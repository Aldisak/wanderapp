using FastEndpoints;
using FluentValidation;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Users.UploadPhoto;

/// <summary>Validates the request body for POST /api/v1/users/me/photos.</summary>
internal sealed class UploadPhotoValidator : Validator<UploadPhotoRequest>
{
    /// <summary>Initialises validation rules.</summary>
    public UploadPhotoValidator()
    {
        RuleFor(x => x.Order)
            .InclusiveBetween(0, ValidationConstants.MaxPhotosPerUser - 1)
            .WithErrorCode(ErrorCodes.Validation.PhotoOrderOutOfRange);
    }
}
