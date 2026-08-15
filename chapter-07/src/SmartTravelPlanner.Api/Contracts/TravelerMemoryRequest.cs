using System.ComponentModel.DataAnnotations;
namespace SmartTravelPlanner.Api.Contracts;
public sealed record TravelerMemoryRequest(
    [MaxLength(20)] IReadOnlyCollection<string>? FoodPreferences = null,
    [MaxLength(20)] IReadOnlyCollection<string>? ActivityInterests = null,
    [StringLength(100)] string? TravelPace = null,
    [StringLength(100)] string? BudgetPreference = null,
    [MaxLength(20)] IReadOnlyCollection<string>? AccessibilityRequirements = null);
