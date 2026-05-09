using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WanderMeet.Infrastructure.Blob;

/// <summary>Azure Blob Storage implementation of <see cref="IBlobStorageService"/>.</summary>
internal sealed class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobStorageOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AzureBlobStorageService> _logger;
    private readonly BlobServiceClient? _serviceClient;

    /// <summary>0 = not initialized; 1 = initialization started or complete.</summary>
    private int _initialized;

    /// <summary>In-flight initialization task; shared across concurrent callers.</summary>
    private Task? _initTask;

    /// <summary>
    /// Initialises the service. When <see cref="BlobStorageOptions.ConnectionString"/> is non-empty,
    /// a <see cref="BlobServiceClient"/> is pre-constructed (but no network calls are made here).
    /// </summary>
    public AzureBlobStorageService(
        IOptions<BlobStorageOptions> options,
        TimeProvider timeProvider,
        ILogger<AzureBlobStorageService> logger)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            // Pin the service version explicitly to one Azurite supports cleanly. Newer SDK
            // defaults send x-ms-version: 2026-02-06 which Azurite < 3.34 rejects with
            // InvalidHeaderValue, and 2025-07-05 has subtle SAS canonicalisation differences
            // that produce AuthorizationFailure on Azurite. 2024-11-04 is the safe baseline.
            var clientOptions = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2024_11_04);
            _serviceClient = new BlobServiceClient(_options.ConnectionString, clientOptions);
        }
    }

    /// <inheritdoc />
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ConnectionString);

    /// <inheritdoc />
    public async Task EnsureContainerExistsAsync(CancellationToken ct)
    {
        ThrowIfNotConfigured();

        // Only one caller should kick off the initialization; the rest await the same task.
        if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 0)
        {
            var containerClient = _serviceClient!.GetBlobContainerClient(_options.ContainerName);
            _initTask = containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
            await _initTask;
            _logger.LogInformation("Ensured blob container {ContainerName} exists", _options.ContainerName);
        }
        else
        {
            // Another caller won the CAS — await its task if it's still in-flight.
            var existingTask = _initTask;
            if (existingTask is not null)
            {
                await existingTask;
            }
        }
    }

    /// <inheritdoc />
    public async Task<BlobSasResult> GenerateWriteSasAsync(string blobPath, TimeSpan ttl, CancellationToken ct)
    {
        ThrowIfNotConfigured();

        await EnsureContainerExistsAsync(ct);

        var containerClient = _serviceClient!.GetBlobContainerClient(_options.ContainerName);
        var blobClient = containerClient.GetBlobClient(blobPath);

        var now = _timeProvider.GetUtcNow();
        var expiresOn = now.Add(ttl);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _options.ContainerName,
            BlobName = blobPath,
            Resource = "b",
            StartsOn = now.AddMinutes(-1),
            ExpiresOn = expiresOn,
            // HTTPS-only (security audit finding F4): a SAS that permits HTTP allows
            // an on-path attacker to replay the signed URL within the TTL window over
            // plaintext. There's no operational reason to allow HTTP for write SAS;
            // both Azurite and real Azure accept HTTPS-only.
            Protocol = SasProtocol.Https,
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Create | BlobSasPermissions.Write);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);
        var blobUrl = blobClient.Uri.ToString();

        _logger.LogInformation("Generated write SAS for blob path in container {ContainerName}", _options.ContainerName);

        return new BlobSasResult(sasUri, expiresOn, blobUrl);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteBlobAsync(string blobPath, CancellationToken ct)
    {
        ThrowIfNotConfigured();

        var containerClient = _serviceClient!.GetBlobContainerClient(_options.ContainerName);
        var blobClient = containerClient.GetBlobClient(blobPath);

        var response = await blobClient.DeleteIfExistsAsync(
            DeleteSnapshotsOption.IncludeSnapshots,
            conditions: null,
            cancellationToken: ct);

        return response.Value;
    }

    private void ThrowIfNotConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "BlobStorageService is not configured. " +
                "Check IsConfigured before calling any blob operation.");
        }
    }
}
