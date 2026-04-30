using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Shared;

namespace WanderMeet.Api.Infrastructure.EntityFramework.Configurations;

internal sealed class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Country).IsRequired().HasMaxLength(2).IsFixedLength();

        builder.Property(x => x.Location)
            .IsRequired()
            .HasColumnType($"geography (Point, {ValidationConstants.GeographySrid})");
        builder.HasIndex(x => x.Location).HasMethod("gist");

        builder.HasIndex(x => new { x.Name, x.Country });
    }
}
