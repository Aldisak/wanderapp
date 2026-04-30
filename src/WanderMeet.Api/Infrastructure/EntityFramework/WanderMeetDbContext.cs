using Microsoft.EntityFrameworkCore;
using WanderMeet.Api.Database.Entities;

namespace WanderMeet.Api.Infrastructure.EntityFramework;

/// <summary>EF Core context for the WanderMeet database (PostgreSQL + PostGIS).</summary>
public sealed class WanderMeetDbContext(DbContextOptions<WanderMeetDbContext> options) : DbContext(options)
{
    /// <summary>End users.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Profile photos.</summary>
    public DbSet<UserPhoto> UserPhotos => Set<UserPhoto>();

    /// <summary>Hangout-type seed data.</summary>
    public DbSet<HangoutTag> HangoutTags => Set<HangoutTag>();

    /// <summary>User ↔ HangoutTag join.</summary>
    public DbSet<UserHangoutTag> UserHangoutTags => Set<UserHangoutTag>();

    /// <summary>Cities reference data.</summary>
    public DbSet<City> Cities => Set<City>();

    /// <summary>User travel history.</summary>
    public DbSet<UserCity> UserCities => Set<UserCity>();

    /// <summary>Places (Google Places synced).</summary>
    public DbSet<Place> Places => Set<Place>();

    /// <summary>Invites between users.</summary>
    public DbSet<Invite> Invites => Set<Invite>();

    /// <summary>Confirmed meetups (1-to-1 with accepted invites).</summary>
    public DbSet<Meetup> Meetups => Set<Meetup>();

    /// <summary>Post-meetup reviews.</summary>
    public DbSet<MeetupReview> MeetupReviews => Set<MeetupReview>();

    /// <summary>User-submitted abuse reports.</summary>
    public DbSet<Report> Reports => Set<Report>();

    /// <summary>Enables PostGIS and applies all <c>IEntityTypeConfiguration</c> classes in this assembly.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WanderMeetDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Enum>().HaveConversion<string>();
        base.ConfigureConventions(configurationBuilder);
    }
}
