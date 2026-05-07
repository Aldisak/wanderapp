using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Users.UploadPhoto;

/// <summary>Integration tests for POST /api/v1/users/me/photos.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class UploadPhotoEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private sealed record UploadPhotoResponseBody(
        Guid PhotoId,
        string BlobUrl,
        string SasUrl,
        DateTimeOffset SasExpiresAt);

    /// <summary>Happy path: known user uploads first photo → 201 with correct body and DB row.</summary>
    [Fact]
    public async Task HandleAsync_KnownUser_Returns201WithPhotoIdBlobUrlSasUrlAndExpiry()
    {
        const string SUB = "oid|upload-happy";
        var expectedNow = App.FakeTimeProvider.GetUtcNow();
        Guid userId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            userId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = userId,
                AzureAdB2CId = SUB,
                FirstName = "Alice",
                CreatedAt = expectedNow,
                LastActiveAt = expectedNow,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.3.0.1");

        var response = await client.PostAsJsonAsync(
            "api/v1/users/me/photos",
            new { Order = 0 },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<UploadPhotoResponseBody>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.PhotoId.Should().NotBeEmpty();
        body.BlobUrl.Should().NotBeNullOrEmpty();
        body.SasUrl.Should().NotBeNullOrEmpty();
        body.SasUrl.Should().Contain("sig="); // SAS token present
        body.SasExpiresAt.Should().BeAfter(expectedNow);
    }

    /// <summary>Happy path: DB row persisted with expected shape.</summary>
    [Fact]
    public async Task HandleAsync_KnownUser_PersistsExactlyOneUserPhotoRowWithExpectedShape()
    {
        const string SUB = "oid|upload-persist";
        var expectedNow = App.FakeTimeProvider.GetUtcNow();
        Guid userId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            userId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = userId,
                AzureAdB2CId = SUB,
                FirstName = "Bob",
                CreatedAt = expectedNow,
                LastActiveAt = expectedNow,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.3.0.2");

        var response = await client.PostAsJsonAsync(
            "api/v1/users/me/photos",
            new { Order = 1 },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<UploadPhotoResponseBody>(
            cancellationToken: TestContext.Current.CancellationToken);

        using var verify = App.Services.CreateScope();
        var db2 = verify.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var photo = await db2.UserPhotos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == body!.PhotoId, TestContext.Current.CancellationToken);

        photo.Should().NotBeNull();
        photo!.UserId.Should().Be(userId);
        photo.Order.Should().Be(1);
        photo.BlobUrl.Should().Contain($"{userId}/photos/{body!.PhotoId}.jpg");
        photo.CreatedAt.Should().Be(expectedNow);
        photo.DeletedAt.Should().BeNull();

        // User.LastActiveAt updated
        var user = await db2.Users.AsNoTracking()
            .FirstAsync(u => u.Id == userId, TestContext.Current.CancellationToken);
        user.LastActiveAt.Should().Be(expectedNow);
    }

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearerToken_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.3.0.3");

        var response = await client.PostAsJsonAsync(
            "api/v1/users/me/photos",
            new { Order = 0 },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Authenticated but no user row → 404 with User.NotRegistered.</summary>
    [Fact]
    public async Task HandleAsync_NoUserRow_Returns404WithUserNotRegistered()
    {
        const string SUB = "oid|upload-no-user";
        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.3.0.4");

        var response = await client.PostAsJsonAsync(
            "api/v1/users/me/photos",
            new { Order = 0 },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.User.NotRegistered);
    }

    /// <summary>Four non-deleted photos already → 400 with PhotoLimitReached.</summary>
    [Fact]
    public async Task HandleAsync_FourNonDeletedPhotos_Returns400WithPhotoLimitReached()
    {
        const string SUB = "oid|upload-limit";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid userId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                AzureAdB2CId = SUB,
                FirstName = "Charlie",
                CreatedAt = now,
                LastActiveAt = now,
            };
            db.Users.Add(user);
            for (var i = 0; i < ValidationConstants.MaxPhotosPerUser; i++)
            {
                db.UserPhotos.Add(new UserPhoto
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Order = i,
                    BlobUrl = $"{userId}/photos/{Guid.NewGuid()}.jpg",
                    CreatedAt = now,
                });
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.3.0.5");

        var response = await client.PostAsJsonAsync(
            "api/v1/users/me/photos",
            new { Order = 0 },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Validation.PhotoLimitReached);
    }

    /// <summary>Order slot already occupied by active photo → 400 with PhotoOrderTaken.</summary>
    [Fact]
    public async Task HandleAsync_OrderSlotAlreadyTakenByNonDeletedPhoto_Returns400WithPhotoOrderTaken()
    {
        const string SUB = "oid|upload-slot-taken";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid userId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            userId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = userId,
                AzureAdB2CId = SUB,
                FirstName = "Dave",
                CreatedAt = now,
                LastActiveAt = now,
            });
            db.UserPhotos.Add(new UserPhoto
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Order = 1,
                BlobUrl = $"{userId}/photos/{Guid.NewGuid()}.jpg",
                CreatedAt = now,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.3.0.6");

        var response = await client.PostAsJsonAsync(
            "api/v1/users/me/photos",
            new { Order = 1 },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.Validation.PhotoOrderTaken);
    }

    /// <summary>Order slot previously soft-deleted → reuse allowed → 201.</summary>
    [Fact]
    public async Task HandleAsync_OrderSlotPreviouslySoftDeletedPhoto_AllowsReuse_Returns201()
    {
        const string SUB = "oid|upload-slot-reuse";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid userId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            userId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = userId,
                AzureAdB2CId = SUB,
                FirstName = "Eve",
                CreatedAt = now,
                LastActiveAt = now,
            });
            // Seed a soft-deleted photo at order 2
            db.UserPhotos.Add(new UserPhoto
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Order = 2,
                BlobUrl = $"{userId}/photos/{Guid.NewGuid()}.jpg",
                CreatedAt = now,
                DeletedAt = now,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.3.0.7");

        var response = await client.PostAsJsonAsync(
            "api/v1/users/me/photos",
            new { Order = 2 },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
