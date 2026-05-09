using WanderMeet.Api.Common;

namespace WanderMeet.Api.Features.Reports;

/// <summary>Feature configuration for the Reports slice.</summary>
internal sealed class ReportsFeatureConfiguration : IFeatureConfiguration
{
    /// <inheritdoc />
    public FeatureInfo Info => new("Reports", "User-to-user report intake (5/day per reporter)");

    /// <inheritdoc />
    public IServiceCollection AddFeatureDependencies(IServiceCollection services, IConfiguration configuration)
        => services;
}
