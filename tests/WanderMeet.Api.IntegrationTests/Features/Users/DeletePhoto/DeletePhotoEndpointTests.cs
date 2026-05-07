using System.Net;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Users.DeletePhoto;

/// <summary>Integration tests for DELETE /api/v1/users/me/photos/{id}.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class DeletePhotoEndpointTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    /// <summary>Pinned to match what AzureBlobStorageService configures.</summary>
    private static BlobClientOptions PinnedClientOptions() =>
        new(BlobClientOptions.ServiceVersion.V2024_11_04);

    /// <summary>No bearer token → 401.</summary>
    [Fact]
    public async Task HandleAsync_NoBearerToken_Returns401()
    {
        var client = App.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.3.1.1");

        var response = await client.DeleteAsync(
            $"api/v1/users/me/photos/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>JWT sub maps to no User row → 404 with User.NotRegistered.</summary>
    [Fact]
    public async Task HandleAsync_NoUserRow_Returns404WithUserNotRegistered()
    {
        var client = App.CreateAuthenticatedClient("oid|deletephoto-nouser");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.3.1.2");

        var response = await client.DeleteAsync(
            $"api/v1/users/me/photos/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(ErrorCodes.User.NotRegistered);
    }

    /// <summary>Photo owned by a different user → 404.</summary>
    [Fact]
    public async Task HandleAsync_PhotoOwnedByDifferentUser_Returns404()
    {
        const string CALLER_SUB = "oid|deletephoto-caller";
        const string OWNER_SUB = "oid|deletephoto-owner";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid photoId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();

            var callerId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = callerId,
                AzureAdB2CId = CALLER_SUB,
                FirstName = "Caller",
                CreatedAt = now,
                LastActiveAt = now,
            });

            var ownerId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = ownerId,
                AzureAdB2CId = OWNER_SUB,
                FirstName = "Owner",
                CreatedAt = now,
                LastActiveAt = now,
            });

            photoId = Guid.NewGuid();
            db.UserPhotos.Add(new UserPhoto
            {
                Id = photoId,
                UserId = ownerId,
                BlobUrl = $"https://example.blob.core.windows.net/photos/{ownerId}/photos/{photoId}.jpg",
                Order = 0,
                CreatedAt = now,
            });

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(CALLER_SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.3.1.3");

        var response = await client.DeleteAsync(
            $"api/v1/users/me/photos/{photoId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Photo already soft-deleted → 404.</summary>
    [Fact]
    public async Task HandleAsync_AlreadySoftDeletedPhoto_Returns404()
    {
        const string SUB = "oid|deletephoto-softdeleted";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid photoId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();

            var userId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = userId,
                AzureAdB2CId = SUB,
                FirstName = "Alice",
                CreatedAt = now,
                LastActiveAt = now,
            });

            photoId = Guid.NewGuid();
            db.UserPhotos.Add(new UserPhoto
            {
                Id = photoId,
                UserId = userId,
                BlobUrl = $"https://example.blob.core.windows.net/photos/{userId}/photos/{photoId}.jpg",
                Order = 0,
                CreatedAt = now,
                DeletedAt = now.AddHours(-1),
            });

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.3.1.4");

        var response = await client.DeleteAsync(
            $"api/v1/users/me/photos/{photoId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Happy path: own active photo → 204, DeletedAt set, blob removed from Azurite.</summary>
    [Fact]
    public async Task HandleAsync_OwnedNonDeletedPhoto_Returns204AndSetsDeletedAt()
    {
        const string SUB = "oid|deletephoto-happy";
        var now = App.FakeTimeProvider.GetUtcNow();
        Guid userId;
        Guid photoId;

        // Arrange: seed user + photo
        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();

            userId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = userId,
                AzureAdB2CId = SUB,
                FirstName = "Bob",
                CreatedAt = now,
                LastActiveAt = now,
            });

            photoId = Guid.NewGuid();
            db.UserPhotos.Add(new UserPhoto
            {
                Id = photoId,
                UserId = userId,
                BlobUrl = $"https://example.blob.core.windows.net/user-photos-tests/{userId}/photos/{photoId}.jpg",
                Order = 0,
                CreatedAt = now,
            });

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Seed the blob in Azurite so DeleteBlobAsync returns true
        var blobPath = $"{userId}/photos/{photoId}.jpg";
        var containerClient = new BlobContainerClient(App.BlobConnectionString, "user-photos-tests", PinnedClientOptions());
        await containerClient.CreateIfNotExistsAsync(cancellationToken: TestContext.Current.CancellationToken);
        var blobClient = containerClient.GetBlobClient(blobPath);
        await blobClient.UploadAsync(BinaryData.FromString("fake-photo"), overwrite: true, TestContext.Current.CancellationToken);

        var client = App.CreateAuthenticatedClient(SUB);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.3.1.5");

        var response = await client.DeleteAsync(
            $"api/v1/users/me/photos/{photoId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert DB: DeletedAt is set
        using var verify = App.Services.CreateScope();
        var db2 = verify.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var photo = await db2.UserPhotos.AsNoTracking()
            .FirstAsync(p => p.Id == photoId, TestContext.Current.CancellationToken);
        photo.DeletedAt.Should().Be(now);

        // Assert Azurite: blob is gone
        var blobExists = await blobClient.ExistsAsync(TestContext.Current.CancellationToken);
        blobExists.Value.Should().BeFalse();
    }
}
