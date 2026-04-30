using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Shared;

namespace WanderMeet.Api.Infrastructure.EntityFramework.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AzureAdB2CId).IsRequired();
        builder.HasIndex(x => x.AzureAdB2CId).IsUnique();

        builder.Property(x => x.FirstName)
            .IsRequired()
            .HasMaxLength(ValidationConstants.FirstNameMaxLength);

        builder.Property(x => x.Bio).HasMaxLength(ValidationConstants.BioMaxLength);

        builder.Property(x => x.Location)
            .HasColumnType($"geography (Point, {ValidationConstants.GeographySrid})");
        builder.HasIndex(x => x.Location).HasMethod("gist");

        builder.HasOne(x => x.City)
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CityId);

        builder.HasIndex(x => new { x.CityId, x.IsOpenToday, x.LastActiveAt });
        builder.HasIndex(x => x.TrustScore);
        builder.HasIndex(x => x.DeletedAt);

        builder.Property(x => x.YearsNomading).HasPrecision(3, 1);
    }
}
