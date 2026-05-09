using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using WanderMeet.Api.Database.Entities;
using WanderMeet.Api.Infrastructure.EntityFramework;
using WanderMeet.Api.Infrastructure.Jobs;
using WanderMeet.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace WanderMeet.Api.IntegrationTests.Infrastructure.Jobs;

/// <summary>Integration tests for <see cref="SinkInactiveProfilesJob"/>.</summary>
[Collection(TestConstants.Collections.PipelineTest)]
public class SinkInactiveProfilesJobTests(IntegrationTestFixture app) : IntegrationTestBase(app)
{
    private static Point CityCenter() => new(14.42, 50.08) { SRID = 4326 };

    private async Task<Guid> SeedCityAsync(DateTimeOffset now)
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var city = new City
        {
            Id = Guid.NewGuid(),
            Name = "Sink City",
            Country = "CZ",
            Location = CityCenter(),
            CreatedAt = now,
        };
        db.Cities.Add(city);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return city.Id;
    }

    private static User MakeUser(string sub, Guid cityId, DateTimeOffset now,
        bool isOpenToday, DateTimeOffset lastActiveAt) => new()
    {
        Id = Guid.NewGuid(),
        AzureAdB2CId = sub,
        FirstName = sub.Length > 8 ? sub[..8] : sub,
        CityId = cityId,
        CreatedAt = now,
        LastActiveAt = lastActiveAt,
        IsOpenToday = isOpenToday,
    };

    /// <summary>Users with IsOpenToday=true and LastActiveAt beyond 24h cutoff are flipped to IsOpenToday=false.</summary>
    [Fact]
    public async Task SinkInactiveProfilesJob_ExecuteAsync_UsersOpenTodayWithLastActiveBeyond24h_AreFlippedToFalse()
    {
        var now = App.FakeTimeProvider.GetUtcNow();
        var cityId = await SeedCityAsync(now);
        var ct = TestContext.Current.CancellationToken;

        Guid userAId, userBId, userCId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();

            // user_a: IsOpenToday=true, inactive for 25h → should be flipped
            var userA = MakeUser("sink-usrA-01", cityId, now, isOpenToday: true, lastActiveAt: now - TimeSpan.FromHours(25));
            // user_b: IsOpenToday=true, active 1h ago → should NOT be flipped
            var userB = MakeUser("sink-usrB-01", cityId, now, isOpenToday: true, lastActiveAt: now - TimeSpan.FromHours(1));
            // user_c: IsOpenToday=false, inactive for 30h → should NOT be touched (already false)
            var userC = MakeUser("sink-usrC-01", cityId, now, isOpenToday: false, lastActiveAt: now - TimeSpan.FromHours(30));

            db.Users.AddRange(userA, userB, userC);
            await db.SaveChangesAsync(ct);
            userAId = userA.Id;
            userBId = userB.Id;
            userCId = userC.Id;
        }

        using var jobScope = App.Services.CreateScope();
        var job = new SinkInactiveProfilesJob(
            jobScope.ServiceProvider.GetRequiredService<WanderMeetDbContext>(),
            jobScope.ServiceProvider.GetRequiredService<TimeProvider>(),
            jobScope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SinkInactiveProfilesJob>>());
        await job.ExecuteAsync(ct);

        using var assertScope = App.Services.CreateScope();
        var db2 = assertScope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var users = await db2.Users.AsNoTracking()
            .Where(u => new[] { userAId, userBId, userCId }.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        users[userAId].IsOpenToday.Should().BeFalse("inactive >24h and was open");
        users[userBId].IsOpenToday.Should().BeTrue("recently active — should not be sunk");
        users[userCId].IsOpenToday.Should().BeFalse("already false — untouched");
    }

    /// <summary>Users with recent activity (LastActiveAt within 24h) are left alone.</summary>
    [Fact]
    public async Task SinkInactiveProfilesJob_ExecuteAsync_UsersWithRecentActivity_LeftAlone()
    {
        var now = App.FakeTimeProvider.GetUtcNow();
        var cityId = await SeedCityAsync(now);
        var ct = TestContext.Current.CancellationToken;

        Guid userId;

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
            var user = MakeUser("sink-recent-01", cityId, now, isOpenToday: true, lastActiveAt: now - TimeSpan.FromHours(23));
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            userId = user.Id;
        }

        using var jobScope = App.Services.CreateScope();
        var job = new SinkInactiveProfilesJob(
            jobScope.ServiceProvider.GetRequiredService<WanderMeetDbContext>(),
            jobScope.ServiceProvider.GetRequiredService<TimeProvider>(),
            jobScope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SinkInactiveProfilesJob>>());
        await job.ExecuteAsync(ct);

        using var assertScope = App.Services.CreateScope();
        var db2 = assertScope.ServiceProvider.GetRequiredService<WanderMeetDbContext>();
        var user2 = await db2.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        user2.IsOpenToday.Should().BeTrue("active within 24h — should not be sunk");
    }
}
