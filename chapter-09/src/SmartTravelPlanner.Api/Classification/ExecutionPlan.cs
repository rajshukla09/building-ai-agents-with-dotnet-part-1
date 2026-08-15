namespace SmartTravelPlanner.Api.Classification;

/// <summary>A typed classifier output and future workflow message.</summary>
public sealed record ExecutionPlan
{
    public required RequestIntent Intent { get; init; }
    public IReadOnlyList<ExecutionStep> Steps { get; init; } = [];
}

public sealed record ExecutionStep
{
    public required int Order { get; init; }
    public required ToolType Tool { get; init; }
    public Dictionary<string, object?> Arguments { get; init; } = [];
}

public enum ToolType
{
    Weather,
    Distance,
    Currency,
    LocalTime
}

public enum RequestIntent
{
    TravelPlanning,
    DistanceLookup,
    CurrencyConversion,
    LocalTime,
    WeatherLookup,
    Unknown
}
