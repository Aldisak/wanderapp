using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Shared;

namespace WanderMeet.Api.Infrastructure.EntityFramework.Configurations;

internal sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Reporter)
            .WithMany()
            .HasForeignKey(x => x.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Reported)
            .WithMany()
            .HasForeignKey(x => x.ReportedId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Reason).IsRequired().HasMaxLength(ValidationConstants.ReportReasonMaxLength);

        builder.HasIndex(x => x.ReporterId);
        builder.HasIndex(x => x.ReportedId);
        builder.HasIndex(x => new { x.ReviewedAt, x.CreatedAt });
    }
}
