using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WanderMeet.Api.Database.Entities;

namespace WanderMeet.Api.Infrastructure.EntityFramework.Configurations;

internal sealed class UserPhotoConfiguration : IEntityTypeConfiguration<UserPhoto>
{
    public void Configure(EntityTypeBuilder<UserPhoto> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.User)
            .WithMany(u => u.Photos)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.BlobUrl).IsRequired();

        // Partial unique index: soft-deleted photos do not occupy a slot, so the user
        // can re-upload to the same Order after deleting an earlier photo.
        builder.HasIndex(x => new { x.UserId, x.Order })
            .IsUnique()
            .HasFilter("\"deleted_at\" IS NULL");
    }
}
