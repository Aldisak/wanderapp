using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Shared;

namespace WanderMeet.Api.Infrastructure.EntityFramework.Configurations;

internal sealed class PlaceConfiguration : IEntityTypeConfiguration<Place>
{
    public void Configure(EntityTypeBuilder<Place> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.GooglePlaceId).IsRequired().HasMaxLength(120);
        builder.HasIndex(x => x.GooglePlaceId).IsUnique();

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);

        builder.HasOne(x => x.City)
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CityId);

        builder.Property(x => x.Location)
            .IsRequired()
            .HasColumnType($"geography (Point, {ValidationConstants.GeographySrid})");
        builder.HasIndex(x => x.Location).HasMethod("gist");

        builder.Property(x => x.GoogleRating).HasPrecision(2, 1);
        builder.Property(x => x.SponsorPerk).HasMaxLength(160);

        builder.HasIndex(x => new { x.CityId, x.Category });
        builder.HasIndex(x => x.WanderMeetupCount);
    }
}
