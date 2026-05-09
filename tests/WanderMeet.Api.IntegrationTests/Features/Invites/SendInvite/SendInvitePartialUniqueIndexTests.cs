using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using WanderMeet.Shared.Enums;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Features.Invites.SendInvite;

/// <summary>
/// Verifies the partial unique index on Invites(SenderId, ReceiverId) WHERE Status='Pending'
/// is registered in the EF Core model AND enforced at the database level. This is the
/// last-line guard against the SendInvite TOCTOU race (security audit finding F5).
/// </summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class SendInvitePartialUniqueIndexTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    /// <summary>EF Core model has the partial-unique index with the expected name and filter.</summary>
    [Fact]
    public void InviteConfiguration_PartialUniquePendingIndex_IsRegistered()
    {
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var inviteEntity = dbContext.Model.FindEntityType(typeof(Invite));

        var index = inviteEntity!.GetIndexes()
            .SingleOrDefault(i => i.GetDatabaseName() == "ix_invites_sender_receiver_pending_unique");

        index.Should().NotBeNull(because: "the partial unique index must be wired in InviteConfiguration");
        index!.IsUnique.Should().BeTrue();
        index.GetFilter().Should().Be("\"status\" = 'Pending'");
        index.Properties.Select(p => p.Name).Should().Equal("SenderId", "ReceiverId");
    }

    /// <summary>
    /// Inserting a second Pending invite for the same (Sender, Receiver) pair via DbContext
    /// — bypassing the endpoint's pre-check — must trigger a unique-violation at the database
    /// (PostgreSQL SQLSTATE 23505). This is what catches the SendInvite TOCTOU race in production.
    /// </summary>
    [Fact]
    public async Task DbInsert_DuplicatePendingInvite_RaisesPgUniqueViolation()
    {
        using var scope = App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();

        var senderId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var hangoutTagId = await SeedHelpers.SeedHangoutTagAsync(dbContext, TestContext.Current.CancellationToken);
        var (placeId, cityId) = await SeedHelpers.SeedCityAndPlaceAsync(dbContext, TestContext.Current.CancellationToken);
        await SeedHelpers.SeedUserAsync(dbContext, senderId, cityId, "sender-sub", TestContext.Current.CancellationToken);
        await SeedHelpers.SeedUserAsync(dbContext, receiverId, cityId, "receiver-sub", TestContext.Current.CancellationToken);

        var now = DateTimeOffset.UtcNow;
        dbContext.Invites.Add(new Invite
        {
            Id = Guid.NewGuid(),
            SenderId = senderId,
            ReceiverId = receiverId,
            HangoutTagId = hangoutTagId,
            PlaceId = placeId,
            SenderIsThere = false,
            Status = InviteStatus.Pending,
            SentAt = now,
            ExpiresAt = now.AddHours(48),
            CreatedAt = now,
        });
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Second Pending insert for the same pair — must violate the partial unique index.
        dbContext.Invites.Add(new Invite
        {
            Id = Guid.NewGuid(),
            SenderId = senderId,
            ReceiverId = receiverId,
            HangoutTagId = hangoutTagId,
            PlaceId = placeId,
            SenderIsThere = false,
            Status = InviteStatus.Pending,
            SentAt = now,
            ExpiresAt = now.AddHours(48),
            CreatedAt = now,
        });

        var act = async () => await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var ex = await act.Should().ThrowAsync<DbUpdateException>(
            because: "the partial unique index must reject a second Pending invite for the same pair");
        ex.Which.InnerException.Should().BeOfType<Npgsql.PostgresException>()
            .Which.SqlState.Should().Be("23505",
                because: "the inner Npgsql exception SQLSTATE 23505 is what SendInviteEndpoint catches to return 409");
    }
}

/// <summary>Tiny helpers for seeding the minimum graph needed by the unique-index test.</summary>
internal static class SeedHelpers
{
    public static async Task<Guid> SeedHangoutTagAsync(WanderMeetDbContext dbContext, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        dbContext.HangoutTags.Add(new HangoutTag
        {
            Id = id,
            Slug = WanderMeet.Shared.Enums.HangoutTagSlug.Coffee,
            Label = "Coffee",
            Emoji = "☕",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync(ct);
        return id;
    }

    public static async Task<(Guid placeId, Guid cityId)> SeedCityAndPlaceAsync(
        WanderMeetDbContext dbContext, CancellationToken ct)
    {
        var cityId = Guid.NewGuid();
        var placeId = Guid.NewGuid();
        var location = new NetTopologySuite.Geometries.Point(14.42, 50.08) { SRID = 4326 };

        dbContext.Cities.Add(new City
        {
            Id = cityId,
            Name = "Test City " + Guid.NewGuid().ToString("N")[..8],
            Country = "CZ",
            Location = location,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        dbContext.Places.Add(new Place
        {
            Id = placeId,
            CityId = cityId,
            GooglePlaceId = "g-" + Guid.NewGuid().ToString("N")[..8],
            Name = "Test Place",
            Category = WanderMeet.Shared.Enums.PlaceCategory.Cafe,
            Location = location,
            HasWifi = true,
            IsQuiet = true,
            IsSoloFriendly = true,
            GoogleRating = 4.5m,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync(ct);
        return (placeId, cityId);
    }

    public static async Task SeedUserAsync(
        WanderMeetDbContext dbContext, Guid id, Guid cityId, string subSuffix, CancellationToken ct)
    {
        dbContext.Users.Add(new User
        {
            Id = id,
            AzureAdB2CId = $"oid|partial-uq-{subSuffix}-{id:N}",
            FirstName = "User-" + id.ToString("N")[..6],
            LastActiveAt = DateTimeOffset.UtcNow,
            CityId = cityId,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync(ct);
    }
}
