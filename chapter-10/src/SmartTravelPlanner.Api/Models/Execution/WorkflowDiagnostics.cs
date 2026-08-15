namespace SmartTravelPlanner.Api.Models.Execution;

/// <summary>A deliberately safe API representation of one typed workflow boundary.</summary>
public sealed record WorkflowMessageTransitionDto
{
    public required int Order { get; init; }
    public required string ExecutorName { get; init; }
    public required string InputMessageType { get; init; }
    public required string OutputMessageType { get; init; }
    public IReadOnlyDictionary < string, object ? >
                                                      CarriedForward {
                                                          get; init;
                                                      } = new Dictionary < string,
                                                      object ? > ();
    public IReadOnlyDictionary < string, object ? > Added { get; init; } = new Dictionary < string, object ? > ();
    public IReadOnlyDictionary<string, WorkflowValueChangeDto> Changed {
        get; init;
    } = new Dictionary<string, WorkflowValueChangeDto>();
    public object ? InputSnapshot { get; init; }
    public object ? OutputSnapshot { get; init; }
}
public sealed record WorkflowValueChangeDto(object? Before, object? After);

public static class DiagnosticRedactor
{
    private static readonly string[] Sensitive =
        ["apikey", "authorization", "connectionstring", "systemprompt", "secret", "traveler-memory"];

    public static IReadOnlyDictionary<string, object?> Redact(IReadOnlyDictionary<string, object?> values) =>
        values.ToDictionary(pair => pair.Key,
                            pair => Sensitive.Any(s => pair.Key.Replace("_", "", StringComparison.Ordinal)
                                                           .Contains(s, StringComparison.OrdinalIgnoreCase))
                                        ? "[REDACTED]"
                                        : pair.Value);
}
