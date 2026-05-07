namespace WanderMeet.Infrastructure.Blob;

/// <summary>Configuration options for Azure Blob Storage access.</summary>
internal sealed record BlobStorageOptions
{
    /// <summary>Azure Storage connection string. Null or empty means blob storage is not configured.</summary>
    public string? ConnectionString { get; init; }

    /// <summary>The blob container name. Defaults to <c>user-photos</c>.</summary>
    public string ContainerName { get; init; } = "user-photos";
}
