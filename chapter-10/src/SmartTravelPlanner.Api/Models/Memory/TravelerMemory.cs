using System.ComponentModel.DataAnnotations;

namespace SmartTravelPlanner.Api.Models.Memory;

public sealed record TravelerMemory
{
    public Guid TravelerId
    {
        get; init;
    }
    [MaxLength(20)] public IReadOnlyCollection<string> FoodPreferences { get; init; } = [];
    [MaxLength(20)] public IReadOnlyCollection<string> ActivityInterests { get; init; } = [];
    [StringLength(100)]
    public string? TravelPace
    {
        get; init;
    }
    [StringLength(100)]
    public string? BudgetPreference
    {
        get; init;
    }
    [MaxLength(20)] public IReadOnlyCollection<string> AccessibilityRequirements { get; init; } = [];
    public DateTimeOffset UpdatedAt
    {
        get; init;
    }
}
