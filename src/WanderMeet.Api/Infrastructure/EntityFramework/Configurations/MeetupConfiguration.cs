using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WanderMeet.Api.Database.Entities;

namespace WanderMeet.Api.Infrastructure.EntityFramework.Configurations;

internal sealed class MeetupConfiguration : IEntityTypeConfiguration<Meetup>
{
    public void Configure(EntityTypeBuilder<Meetup> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Invite)
            .WithOne()
            .HasForeignKey<Meetup>(x => x.InviteId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.InviteId).IsUnique();

        builder.HasOne(x => x.UserA)
            .WithMany()
            .HasForeignKey(x => x.UserAId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UserB)
            .WithMany()
            .HasForeignKey(x => x.UserBId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Place)
            .WithMany()
            .HasForeignKey(x => x.PlaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.UserAId);
        builder.HasIndex(x => x.UserBId);
        builder.HasIndex(x => x.PlaceId);
        builder.HasIndex(x => new { x.PromptSent, x.MetAt });
    }
}
