using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Infrastructure.Blob;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Users.UploadPhoto;

/// <summary>Creates a pre-registered photo slot and issues a write-only SAS URI for the blob upload.</summary>
internal sealed class UploadPhotoEndpoint(
    WanderMeetDbContext dbContext,
    IBlobStorageService blobStorage,
    TimeProvider timeProvider)
    : Endpoint<UploadPhotoRequest, UploadPhotoResponse>
{
    private readonly UsersFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Post("users/me/photos");
        Description(b => b
            .WithName(nameof(UploadPhotoEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "Upload a user profile photo";
            s.Description = "Pre-creates a UserPhoto row and returns a write-only SAS URI for uploading the blob. The caller must PUT the binary content to the SAS URL within the expiry window.";
            s.Responses[StatusCodes.Status201Created] = "Photo slot created; SAS URI returned";
            s.Responses[StatusCodes.Status400BadRequest] = "Photo limit reached or order slot already taken";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "Caller has no user profile (User.NotRegistered)";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
            s.Responses[StatusCodes.Status503ServiceUnavailable] = "Blob storage is not configured (Storage.NotConfigured)";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(UploadPhotoRequest req, CancellationToken ct)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sub))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.AzureAdB2CId == sub && u.DeletedAt == null, ct);

        if (user is null)
        {
            AddError(ErrorCodes.User.NotRegistered, "No user profile found for this identity.");
            await Send.ErrorsAsync(404, ct);
            return;
        }

        if (!blobStorage.IsConfigured)
        {
            AddError(ErrorCodes.Storage.NotConfigured, "Blob storage is not configured.");
            await Send.ErrorsAsync(503, ct);
            return;
        }

        var activeOrders = await dbContext.UserPhotos
            .AsNoTracking()
            .Where(p => p.UserId == user.Id && p.DeletedAt == null)
            .Select(p => p.Order)
            .ToListAsync(ct);

        if (activeOrders.Count >= ValidationConstants.MaxPhotosPerUser)
        {
            AddError(ErrorCodes.Validation.PhotoLimitReached, "Maximum number of photos reached.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        if (activeOrders.Contains(req.Order))
        {
            AddError(ErrorCodes.Validation.PhotoOrderTaken, "This photo order slot is already occupied.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var photoId = Guid.NewGuid();
        var blobPath = $"{user.Id}/photos/{photoId}.jpg";

        var sas = await blobStorage.GenerateWriteSasAsync(blobPath, TimeSpan.FromMinutes(10), ct);

        var now = timeProvider.GetUtcNow();
        var photo = new UserPhoto
        {
            Id = photoId,
            UserId = user.Id,
            Order = req.Order,
            BlobUrl = sas.BlobUrl,
            CreatedAt = now,
        };

        dbContext.UserPhotos.Add(photo);
        user.LastActiveAt = now;
        await dbContext.SaveChangesAsync(ct);

        await Send.ResponseAsync(
            new UploadPhotoResponse(photoId, sas.BlobUrl, sas.SasUrl.ToString(), sas.ExpiresAt),
            StatusCodes.Status201Created,
            ct);
    }
}
