namespace WanderMeet.Api.Common;

/// <summary>Swagger tag info for a vertical-slice feature.</summary>
/// <param name="Name">Short tag name shown in Swagger UI.</param>
/// <param name="Description">One-line description of the feature.</param>
public sealed record FeatureInfo(string Name, string Description);
