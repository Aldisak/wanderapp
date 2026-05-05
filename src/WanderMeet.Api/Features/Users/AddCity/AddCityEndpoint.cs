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

namespace WanderMeet.Api.Features.Users.AddCity;

/// <summary>Adds a city visit to the authenticated user's travel history.</summary>
internal sealed class AddCityEndpoint(WanderMeetDbContext dbContext, TimeProvider timeProvider)
    : Endpoint<AddCityRequest, UserCityDto>
{
    private readonly UsersFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Post("users/me/cities");
        Description(b => b
            .WithName(nameof(AddCityEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.GeneralApi));
        DontCatchExceptions();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "Add a city visit";
            s.Description = "Records a new row in the authenticated user's travel history. The user's CitiesCount is incremented.";
            s.Responses[StatusCodes.Status201Created] = "Travel history row created";
            s.Responses[StatusCodes.Status400BadRequest] = "Validation error or unknown city id";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status404NotFound] = "Caller has no user profile (User.NotRegistered)";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(AddCityRequest req, CancellationToken ct)
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

        var cityExists = await dbContext.Cities
            .AsNoTracking()
            .AnyAsync(c => c.Id == req.CityId && c.DeletedAt == null, ct);

        if (!cityExists)
        {
            AddError(ErrorCodes.UserValidation.CityIdNotFound, "Unknown city id.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var now = timeProvider.GetUtcNow();
        var userCity = new UserCity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CityId = req.CityId,
            ArrivedAt = req.ArrivedAt,
            DepartedAt = null,
            CreatedAt = now,
        };

        dbContext.UserCities.Add(userCity);
        user.CitiesCount += 1;
        user.LastActiveAt = now;
        await dbContext.SaveChangesAsync(ct);

        await Send.ResponseAsync(
            new UserCityDto(userCity.Id, userCity.CityId, userCity.ArrivedAt, userCity.DepartedAt),
            StatusCodes.Status201Created,
            ct);
    }
}
