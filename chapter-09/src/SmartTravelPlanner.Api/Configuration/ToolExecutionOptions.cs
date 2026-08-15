using System.ComponentModel.DataAnnotations;

namespace SmartTravelPlanner.Api.Configuration;

public sealed class ToolExecutionOptions
{
    public const string SectionName = "ToolExecution";

    [Range(0, 3)]
    public int MaximumRetries { get; init; } = 3;

    [Range(1, 60)]
    public int TimeoutSeconds { get; init; } = 5;
}
