using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WanderMeet.Infrastructure.Blob;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Infrastructure.Blob;

/// <summary>
/// Smoke tests verifying <see cref="AzureBlobStorageService"/> against a live Azurite container.
/// These tests exercise SAS generation, upload, deletion, and SAS scope security.
/// </summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class AzureBlobSmokeTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    /// <summary>Pinned to match what AzureBlobStorageService configures — Azurite ≤ 3.31 rejects the SDK default.</summary>
    private static BlobClientOptions PinnedClientOptions() =>
        new(BlobClientOptions.ServiceVersion.V2024_11_04);

    [Fact]
    public async Task GenerateWriteSasAsync_ValidBlobPath_ReturnsSasUrlAndBlobUrl()
    {
        var service = App.Services.GetRequiredService<IBlobStorageService>();
        var blobPath = $"{Guid.NewGuid()}/photos/{Guid.NewGuid()}.jpg";
        var ttl = TimeSpan.FromMinutes(10);

        var result = await service.GenerateWriteSasAsync(blobPath, ttl, TestContext.Current.CancellationToken);

        result.SasUrl.Should().NotBeNull();
        result.SasUrl.Query.Should().Contain("sig=");
        result.BlobUrl.Should().NotBeNullOrEmpty();
        result.BlobUrl.Should().NotContain("sig=");
        result.ExpiresAt.Should().BeAfter(App.FakeTimeProvider.GetUtcNow());
    }

    [Fact]
    public async Task GenerateWriteSasAsync_ThenUploadViaSas_Succeeds()
    {
        var service = App.Services.GetRequiredService<IBlobStorageService>();
        var blobPath = $"{Guid.NewGuid()}/photos/{Guid.NewGuid()}.jpg";

        var result = await service.GenerateWriteSasAsync(blobPath, TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);

        // Upload using the SAS URI — this bypasses the connection string entirely
        var sasClient = new BlobClient(result.SasUrl, PinnedClientOptions());
        var content = BinaryData.FromString("fake-image-content");
        var uploadResponse = await sasClient.UploadAsync(content, overwrite: true, TestContext.Current.CancellationToken);

        uploadResponse.GetRawResponse().Status.Should().Be(201);
    }

    [Fact]
    public async Task DeleteBlobAsync_ExistingBlob_ReturnsTrueAndBlobIsGone()
    {
        // Arrange: create a blob directly via connection string
        var options = App.Services.GetRequiredService<IOptions<BlobStorageOptions>>().Value;
        var containerClient = new BlobContainerClient(App.BlobConnectionString, options.ContainerName, PinnedClientOptions());
        await containerClient.CreateIfNotExistsAsync(cancellationToken: TestContext.Current.CancellationToken);

        var blobPath = $"{Guid.NewGuid()}/photos/{Guid.NewGuid()}.jpg";
        var blobClient = containerClient.GetBlobClient(blobPath);
        await blobClient.UploadAsync(BinaryData.FromString("to-delete"), overwrite: true, TestContext.Current.CancellationToken);

        var service = App.Services.GetRequiredService<IBlobStorageService>();

        // Act
        var deleted = await service.DeleteBlobAsync(blobPath, TestContext.Current.CancellationToken);

        // Assert
        deleted.Should().BeTrue();
        var existsResponse = await blobClient.ExistsAsync(TestContext.Current.CancellationToken);
        existsResponse.Value.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateWriteSasAsync_SasUri_BlocksPutToDifferentBlobInSameContainer()
    {
        // Arrange: generate a SAS for one specific blob
        var service = App.Services.GetRequiredService<IBlobStorageService>();
        var targetBlobPath = $"{Guid.NewGuid()}/photos/{Guid.NewGuid()}.jpg";
        var siblingBlobPath = $"{Guid.NewGuid()}/photos/{Guid.NewGuid()}.jpg";

        var result = await service.GenerateWriteSasAsync(targetBlobPath, TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);

        // Build a URI pointing to the SIBLING blob but using the TARGET blob's SAS query string
        var sasQuery = result.SasUrl.Query;
        var options = App.Services.GetRequiredService<IOptions<BlobStorageOptions>>().Value;

        // Reconstruct: take the base service URI + container + siblingPath + sasQuery from targetBlob SAS
        var serviceUri = new BlobServiceClient(App.BlobConnectionString, PinnedClientOptions()).Uri;
        var siblingUriWithTargetSas = new Uri($"{serviceUri}{options.ContainerName}/{siblingBlobPath}{sasQuery}");

        var siblingClientWithWrongSas = new BlobClient(siblingUriWithTargetSas, PinnedClientOptions());

        // Act: attempt PUT to sibling blob using the target blob's SAS — must be rejected
        var act = async () => await siblingClientWithWrongSas.UploadAsync(
            BinaryData.FromString("attacker-content"),
            overwrite: true,
            TestContext.Current.CancellationToken);

        // Assert: the SAS must not authorise writes to a sibling blob.
        // Azure production returns 403 (AuthorizationFailure); Azurite returns 404 because it
        // surfaces the path mismatch as "not found" rather than an auth error. Both outcomes
        // prove the SAS scope is single-blob and not container-wide — the security guarantee.
        await act.Should().ThrowAsync<RequestFailedException>()
            .Where(ex => ex.Status == 403 || ex.Status == 404);
    }
}
