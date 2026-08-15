using System.ComponentModel.DataAnnotations;

namespace SmartTravelPlanner.Api.Configuration;

public sealed class SessionLifecycleOptions
{
    public const string SectionName = "SessionLifecycle";

    [Range(1, 1_440)]
    public int IdleTimeoutMinutes { get; init; } = 15;

    [Range(2, 10_080)]
    public int ExpirationTimeoutMinutes { get; init; } = 30;

    public TimeSpan IdleTimeout => TimeSpan.FromMinutes(IdleTimeoutMinutes);

    public TimeSpan ExpirationTimeout => TimeSpan.FromMinutes(ExpirationTimeoutMinutes);
}
