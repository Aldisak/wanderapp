using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Authorization;
using WanderMeet.Api.Common;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Auth.Register;

/// <summary>Registers a new user profile tied to the caller's Azure AD B2C identity.</summary>
internal sealed class RegisterEndpoint(WanderMeetDbContext dbContext, TimeProvider timeProvider)
    : Endpoint<RegisterRequest, RegisterResponse>
{
    private readonly AuthFeatureConfiguration _featureConfiguration = new();

    /// <inheritdoc />
    public override void Configure()
    {
        Post("auth/register");
        Description(b => b
            .WithName(nameof(RegisterEndpoint))
            .WithTags(_featureConfiguration.Info.Name)
            .RequireRateLimiting(RateLimitPolicies.AuthEndpoints));
        DontCatchExceptions();
        Policies(nameof(AuthorizationPolicies.UsersOnly));

        Summary(s =>
        {
            s.Summary = "Register a new user profile";
            s.Description = "Creates a local user profile linked to the caller's Azure AD B2C identity (sub claim).";
            s.Responses[StatusCodes.Status201Created] = "User profile created";
            s.Responses[StatusCodes.Status400BadRequest] = "Validation error (FirstName missing or too long)";
            s.Responses[StatusCodes.Status401Unauthorized] = "Bearer token missing or invalid";
            s.Responses[StatusCodes.Status409Conflict] = "A profile already exists for this Azure AD B2C identity";
            s.Responses[StatusCodes.Status429TooManyRequests] = "Rate limit exceeded";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(RegisterRequest req, CancellationToken ct)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sub))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var alreadyExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.AzureAdB2CId == sub, ct);

        if (alreadyExists)
        {
            AddError(ErrorCodes.Auth.AlreadyRegistered, "A profile with this identity already exists.");
            await Send.ErrorsAsync(409, ct);
            return;
        }

        var now = timeProvider.GetUtcNow();
        var user = new User
        {
            Id = Guid.NewGuid(),
            AzureAdB2CId = sub,
            FirstName = req.FirstName,
            CreatedAt = now,
            LastActiveAt = now,
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(ct);

        await Send.CreatedAtAsync<RegisterEndpoint>(
            routeValues: null,
            responseBody: ToResponse(user),
            generateAbsoluteUrl: false,
            cancellation: ct);
    }

    private static RegisterResponse ToResponse(User user) =>
        new(
            user.Id,
            user.FirstName,
            user.IsIdVerified,
            user.IsOpenToday,
            user.IsOpenToRomance,
            user.TrustScore,
            user.MeetupCount,
            user.CitiesCount,
            user.CreatedAt);
}
