using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Features.Users.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Users.UpdateMe;

/// <summary>Updates the authenticated user's own profile.</summary>
internal sealed class UpdateMeEndpoint(WanderMeetDbContext dbContext, TimeProvider timeProvider)
    : Endpoint<UpdateMeRequest, UserDto>
{
    private readonly UsersFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Patch("users/me");
        Description(b => b
            .WithName(nameof(UpdateMeEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "Update own profile";
            s.Description = "Partially updates the authenticated user's profile. Only provided fields are applied.";
            s.Responses[StatusCodes.Status200OK] = "Updated user profile";
            s.Responses[StatusCodes.Status400BadRequest] = "Validation error";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "No user profile found for this identity";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(UpdateMeRequest req, CancellationToken ct)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sub))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var now = timeProvider.GetUtcNow();

        var user = await dbContext.Users
            .Include(u => u.HangoutTags)
            .FirstOrDefaultAsync(u => u.AzureAdB2CId == sub && u.DeletedAt == null, ct);

        if (user is null)
        {
            AddError(ErrorCodes.User.NotRegistered, "No user profile found for this identity.");
            await Send.ErrorsAsync(404, ct);
            return;
        }

        // Validate and replace hangout tags when provided
        if (req.HangoutTagIds is not null)
        {
            var existingTagIds = await dbContext.HangoutTags
                .AsNoTracking()
                .Where(ht => req.HangoutTagIds.Contains(ht.Id))
                .Select(ht => ht.Id)
                .ToListAsync(ct);

            var missingIds = req.HangoutTagIds.Except(existingTagIds).ToList();
            if (missingIds.Count > 0)
            {
                AddError(ErrorCodes.UserValidation.HangoutTagIdNotFound, "One or more hangout tag IDs do not exist.");
                await Send.ErrorsAsync(400, ct);
                return;
            }

            // Delete all existing hangout tags via bulk operation, then clear tracked collection
            await dbContext.UserHangoutTags
                .Where(uht => uht.UserId == user.Id)
                .ExecuteDeleteAsync(ct);
            user.HangoutTags.Clear();

            foreach (var tagId in req.HangoutTagIds)
            {
                dbContext.UserHangoutTags.Add(new UserHangoutTag
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    HangoutTagId = tagId,
                    CreatedAt = now,
                });
            }
        }

        if (req.Bio is not null) user.Bio = req.Bio;
        if (req.IsOpenToRomance.HasValue) user.IsOpenToRomance = req.IsOpenToRomance.Value;
        user.LastActiveAt = now;

        await dbContext.SaveChangesAsync(ct);

        var dto = new UserDto(
            user.Id,
            user.FirstName,
            user.Bio,
            user.IsIdVerified,
            user.IsOpenToday,
            user.IsOpenToRomance,
            user.LastActiveAt,
            user.TrustScore,
            user.MeetupCount,
            user.CitiesCount,
            user.YearsNomading,
            user.CityId,
            user.CreatedAt,
            user.HangoutTags.Select(ht => ht.HangoutTagId).ToList());

        await Send.OkAsync(dto, ct);
    }
}
