using Microsoft.EntityFrameworkCore;

namespace WanderMeet.Api.Infrastructure.EntityFramework;

/// <summary>EF Core context for the WanderMeet database (PostgreSQL + PostGIS).</summary>
public sealed class WanderMeetDbContext(DbContextOptions<WanderMeetDbContext> options) : DbContext(options)
{
    /// <summary>Enables the PostGIS extension and stores enums as strings globally.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        base.OnModelCreating(modelBuilder);
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Enum>().HaveConversion<string>();
        base.ConfigureConventions(configurationBuilder);
    }
}
