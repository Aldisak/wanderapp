using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Infrastructure.EntityFramework.Configurations;

internal sealed class HangoutTagConfiguration : IEntityTypeConfiguration<HangoutTag>
{
    private static readonly DateTimeOffset SeedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<HangoutTag> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Slug).IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique();

        builder.Property(x => x.Label).IsRequired().HasMaxLength(40);
        builder.Property(x => x.Emoji).IsRequired().HasMaxLength(8);

        builder.HasData(
            new HangoutTag
            {
                Id = new Guid("00000000-0000-0000-0000-000000000001"),
                Slug = HangoutTagSlug.Coffee,
                Label = "Coffee",
                Emoji = "☕",
                CreatedAt = SeedTimestamp
            },
            new HangoutTag
            {
                Id = new Guid("00000000-0000-0000-0000-000000000002"),
                Slug = HangoutTagSlug.Walk,
                Label = "Walk",
                Emoji = "\U0001F6B6",
                CreatedAt = SeedTimestamp
            },
            new HangoutTag
            {
                Id = new Guid("00000000-0000-0000-0000-000000000003"),
                Slug = HangoutTagSlug.Food,
                Label = "Food",
                Emoji = "\U0001F37D️",
                CreatedAt = SeedTimestamp
            },
            new HangoutTag
            {
                Id = new Guid("00000000-0000-0000-0000-000000000004"),
                Slug = HangoutTagSlug.Explore,
                Label = "Explore",
                Emoji = "\U0001F5FA️",
                CreatedAt = SeedTimestamp
            },
            new HangoutTag
            {
                Id = new Guid("00000000-0000-0000-0000-000000000005"),
                Slug = HangoutTagSlug.Cowork,
                Label = "Cowork",
                Emoji = "\U0001F4BB",
                CreatedAt = SeedTimestamp
            });
    }
}
