using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WanderMeet.Api.Database.Entities;

namespace WanderMeet.Api.Infrastructure.EntityFramework.Configurations;

internal sealed class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Sender)
            .WithMany()
            .HasForeignKey(x => x.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Receiver)
            .WithMany()
            .HasForeignKey(x => x.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.HangoutTag)
            .WithMany()
            .HasForeignKey(x => x.HangoutTagId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Place)
            .WithMany()
            .HasForeignKey(x => x.PlaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SenderId);
        builder.HasIndex(x => x.ReceiverId);
        builder.HasIndex(x => x.HangoutTagId);
        builder.HasIndex(x => x.PlaceId);

        builder.HasIndex(x => new { x.ReceiverId, x.Status });
        builder.HasIndex(x => new { x.SenderId, x.Status });
        builder.HasIndex(x => new { x.Status, x.ExpiresAt });

        // Partial unique index — at most one Pending invite per (sender, receiver) pair.
        // Eliminates the TOCTOU race in SendInviteEndpoint where two concurrent POSTs both
        // pass the AnyAsync pre-check before either calls SaveChangesAsync (security audit
        // finding F5). The endpoint catches the resulting DbUpdateException → 23505 → 409.
        builder.HasIndex(x => new { x.SenderId, x.ReceiverId })
            .IsUnique()
            .HasFilter("\"status\" = 'Pending'")
            .HasDatabaseName("ix_invites_sender_receiver_pending_unique");
    }
}
