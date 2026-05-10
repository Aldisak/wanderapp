using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Infrastructure.Blob;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Users.DeletePhoto;

/// <summary>
/// Soft-deletes the caller's photo and removes the backing blob from storage (best-effort).
/// No body validator is required — the {id:guid} route constraint covers shape.
/// </summary>
internal sealed class DeletePhotoEndpoint(
    WanderMeetDbContext dbContext,
    TimeProvider timeProvider,
    IBlobStorageService blobStorage,
    ILogger<DeletePhotoEndpoint> logger)
    : Endpoint<DeletePhotoRequest>
{
    private readonly UsersFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Delete("users/me/photos/{id:guid}");
        Description(b => b
            .WithName(nameof(DeletePhotoEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "Delete a photo";
            s.Description = "Soft-deletes the caller's photo and removes the backing blob (best-effort). The {id:guid} route constraint covers input shape — no separate validator.";
            s.Responses[StatusCodes.Status204NoContent] = "Photo deleted";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "Photo not found, not owned by the caller, or already deleted";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
            s.Responses[StatusCodes.Status503ServiceUnavailable] = "Blob storage is not configured";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(DeletePhotoRequest req, CancellationToken ct)
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
            AddError(ErrorCodes.User.NotRegistered, "No user profile found for the authenticated identity.");
            await Send.ErrorsAsync(404, ct);
            return;
        }

        if (!blobStorage.IsConfigured)
        {
            AddError(ErrorCodes.Storage.NotConfigured, "Blob storage is not configured.");
            await Send.ErrorsAsync(503, ct);
            return;
        }

        var photo = await dbContext.UserPhotos
            .FirstOrDefaultAsync(p => p.Id == req.Id && p.UserId == user.Id && p.DeletedAt == null, ct);

        if (photo is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var now = timeProvider.GetUtcNow();
        photo.DeletedAt = now;
        user.LastActiveAt = now;
        await dbContext.SaveChangesAsync(ct);

        // Best-effort blob removal — log warning on failure, never bubble to global handler
        var blobPath = $"{user.Id}/photos/{photo.Id}.jpg";
        try
        {
            await blobStorage.DeleteBlobAsync(blobPath, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Photo blob delete failed (best-effort) {PhotoId} {UserId}", photo.Id, user.Id);
        }

        await Send.NoContentAsync(ct);
    }
}
