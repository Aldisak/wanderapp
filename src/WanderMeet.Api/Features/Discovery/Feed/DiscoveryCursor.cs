using System.Text.Json;

namespace WanderMeet.Api.Features.Discovery.Feed;

/// <summary>Opaque keyset cursor for the discovery feed — base64 over canonical JSON.</summary>
internal readonly record struct DiscoveryCursor(
    DateTimeOffset LastActiveAt,
    int TrustScore,
    Guid Id,
    bool IsOpenToday)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Encodes a cursor to an opaque base64 string.</summary>
    public static string Encode(DiscoveryCursor cursor)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(cursor, JsonOptions);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>Decodes a cursor from a base64 string. Returns false if decoding fails.</summary>
    public static bool TryDecode(string? encoded, out DiscoveryCursor cursor)
    {
        cursor = default;
        if (string.IsNullOrEmpty(encoded))
            return false;

        try
        {
            var bytes = Convert.FromBase64String(encoded);
            var decoded = JsonSerializer.Deserialize<DiscoveryCursorPayload>(bytes, JsonOptions);
            if (decoded?.LastActiveAt is null || decoded.TrustScore is null
                || decoded.Id is null || decoded.IsOpenToday is null)
                return false;

            cursor = new DiscoveryCursor(
                decoded.LastActiveAt.Value,
                decoded.TrustScore.Value,
                decoded.Id.Value,
                decoded.IsOpenToday.Value);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return false;
        }
    }

    /// <summary>Intermediate deserialization target to detect missing required fields.</summary>
    private sealed class DiscoveryCursorPayload
    {
        public DateTimeOffset? LastActiveAt { get; init; }
        public int? TrustScore { get; init; }
        public Guid? Id { get; init; }
        public bool? IsOpenToday { get; init; }
    }
}
