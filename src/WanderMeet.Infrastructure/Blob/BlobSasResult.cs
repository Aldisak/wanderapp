namespace WanderMeet.Infrastructure.Blob;

/// <summary>Result of a write SAS generation operation for a single blob.</summary>
/// <param name="SasUrl">The SAS URI scoped to the single blob with Create and Write permissions.</param>
/// <param name="ExpiresAt">The UTC expiry time of the SAS token.</param>
/// <param name="BlobUrl">The canonical blob URL (without SAS query parameters).</param>
public record BlobSasResult(Uri SasUrl, DateTimeOffset ExpiresAt, string BlobUrl);
