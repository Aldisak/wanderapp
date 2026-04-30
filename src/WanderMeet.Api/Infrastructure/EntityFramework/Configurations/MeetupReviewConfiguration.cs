using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Shared;

namespace WanderMeet.Api.Infrastructure.EntityFramework.Configurations;

internal sealed class MeetupReviewConfiguration : IEntityTypeConfiguration<MeetupReview>
{
    public void Configure(EntityTypeBuilder<MeetupReview> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Meetup)
            .WithMany()
            .HasForeignKey(x => x.MeetupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Reviewer)
            .WithMany()
            .HasForeignKey(x => x.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Reviewee)
            .WithMany()
            .HasForeignKey(x => x.RevieweeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Text).HasMaxLength(ValidationConstants.ReviewTextMaxLength);

        builder.HasIndex(x => x.MeetupId);
        builder.HasIndex(x => x.RevieweeId);
        builder.HasIndex(x => new { x.MeetupId, x.ReviewerId }).IsUnique();
    }
}
