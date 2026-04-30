using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace WanderMeet.Api.Infrastructure.EntityFramework;

/// <summary>
/// Used by <c>dotnet ef</c> tooling at design-time so migrations can be added
/// without booting the full Web host. Reads the connection string from
/// <c>appsettings.Development.json</c> by default.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<WanderMeetDbContext>
{
    /// <inheritdoc />
    public WanderMeetDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=wandermeet;Username=wandermeet;Password=wandermeet";

        var options = new DbContextOptionsBuilder<WanderMeetDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new WanderMeetDbContext(options);
    }
}
