namespace WanderMeet.Infrastructure.Blob;

/// <summary>Configuration options for Azure Blob Storage access.</summary>
internal sealed record BlobStorageOptions
{
    /// <summary>Azure Storage connection string. Null or empty means blob storage is not configured.</summary>
    public string? ConnectionString { get; init; }

    /// <summary>The blob container name. Defaults to <c>user-photos</c>.</summary>
    public string ContainerName { get; init; } = "user-photos";

    /// <summary>
    /// SAS protocol restriction. Defaults to HTTPS-only (security audit finding F4).
    /// Tests targeting Azurite (HTTP-only by default) override to <c>HttpsAndHttp</c>.
    /// </summary>
    public Azure.Storage.Sas.SasProtocol SasProtocol { get; init; } = Azure.Storage.Sas.SasProtocol.Https;
}
