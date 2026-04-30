namespace WanderMeet.Api.Database.Entities;

/// <summary>One stay in a city — a row in the user's travel history.</summary>
public class UserCity : AuditableEntity
{
    /// <summary>User side.</summary>
    public required Guid UserId { get; set; }

    /// <inheritdoc cref="UserId" />
    public User? User { get; set; }

    /// <summary>City side.</summary>
    public required Guid CityId { get; set; }

    /// <inheritdoc cref="CityId" />
    public City? City { get; set; }

    /// <summary>UTC timestamp when the user arrived in the city.</summary>
    public required DateTimeOffset ArrivedAt { get; set; }

    /// <summary>UTC timestamp when the user left; <c>null</c> means currently here.</summary>
    public DateTimeOffset? DepartedAt { get; set; }
}
