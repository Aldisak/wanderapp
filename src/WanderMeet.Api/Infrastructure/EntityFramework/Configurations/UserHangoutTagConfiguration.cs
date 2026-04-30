using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WanderMeet.Api.Database.Entities;

namespace WanderMeet.Api.Infrastructure.EntityFramework.Configurations;

internal sealed class UserHangoutTagConfiguration : IEntityTypeConfiguration<UserHangoutTag>
{
    public void Configure(EntityTypeBuilder<UserHangoutTag> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.User)
            .WithMany(u => u.HangoutTags)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.HangoutTag)
            .WithMany()
            .HasForeignKey(x => x.HangoutTagId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.HangoutTagId }).IsUnique();
        builder.HasIndex(x => x.HangoutTagId);
    }
}
