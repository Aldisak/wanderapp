namespace WanderMeet.Infrastructure.Blob;

/// <summary>
/// Abstraction over Azure Blob Storage for user-photo operations.
/// Implementations MUST NOT log the connection string or any returned SAS URI at any log level.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Returns <c>true</c> when <see cref="BlobStorageOptions.ConnectionString"/> is non-null and non-empty.
    /// Endpoints MUST check this property and emit 503 before calling any other method.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Generates a write SAS URI scoped to <paramref name="blobPath"/> only, with
    /// <c>Create | Write</c> permissions and the given TTL.
    /// Creates the container on first call if it does not yet exist (lazy init, thread-safe).
    /// </summary>
    /// <param name="blobPath">Relative path within the container (e.g. <c>{userId}/photos/{photoId}.jpg</c>).</param>
    /// <param name="ttl">How long the SAS token should remain valid.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<BlobSasResult> GenerateWriteSasAsync(string blobPath, TimeSpan ttl, CancellationToken ct);

    /// <summary>
    /// Deletes <paramref name="blobPath"/> from the container.
    /// Returns <c>false</c> if the blob did not exist; never throws on 404.
    /// </summary>
    /// <param name="blobPath">Relative path within the container.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> DeleteBlobAsync(string blobPath, CancellationToken ct);

    /// <summary>Ensures the storage container exists. Idempotent.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task EnsureContainerExistsAsync(CancellationToken ct);
}
