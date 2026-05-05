using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Features.Users.Shared;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Users.UpdateCity;

/// <summary>Updates a row in the authenticated user's travel history (typically to set DepartedAt).</summary>
internal sealed class UpdateCityEndpoint(WanderMeetDbContext dbContext, TimeProvider timeProvider)
    : Endpoint<UpdateCityRequest, UserCityDto>
{
    private readonly UsersFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Patch("users/me/cities/{id:guid}");
        Description(b => b
            .WithName(nameof(UpdateCityEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "Update a travel-history row";
            s.Description = "Sets DepartedAt on the caller's UserCity row. The row must belong to the calling user.";
            s.Responses[StatusCodes.Status200OK] = "Travel-history row updated";
            s.Responses[StatusCodes.Status400BadRequest] = "Validation error or DepartedAt < ArrivedAt";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "Row not found or not owned by the caller";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(UpdateCityRequest req, CancellationToken ct)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sub))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var userCity = await dbContext.UserCities
            .Include(uc => uc.User)
            .FirstOrDefaultAsync(uc => uc.Id == req.Id && uc.User!.AzureAdB2CId == sub && uc.User.DeletedAt == null, ct);

        if (userCity is null)
        {
            AddError(ErrorCodes.UserCity.NotFound, "Travel-history row not found.");
            await Send.ErrorsAsync(404, ct);
            return;
        }

        if (req.DepartedAt is { } departedAt && departedAt < userCity.ArrivedAt)
        {
            AddError(ErrorCodes.UserValidation.DepartedAtBeforeArrived, "DepartedAt must be on or after ArrivedAt.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var now = timeProvider.GetUtcNow();
        userCity.DepartedAt = req.DepartedAt;
        userCity.UpdatedAt = now;
        userCity.User!.LastActiveAt = now;
        await dbContext.SaveChangesAsync(ct);

        await Send.OkAsync(
            new UserCityDto(userCity.Id, userCity.CityId, userCity.ArrivedAt, userCity.DepartedAt),
            ct);
    }
}
