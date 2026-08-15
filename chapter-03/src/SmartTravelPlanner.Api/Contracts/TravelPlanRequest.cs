using System.ComponentModel.DataAnnotations;

namespace SmartTravelPlanner.Api.Contracts;

public sealed record TravelPlanRequest(
    [Required, StringLength(100)] string Destination,
    [Range(1, 14)] int DurationDays,
    [StringLength(500)] string? Preferences = null)
{
    public const int MaximumDurationDays = 14;
}
