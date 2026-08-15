namespace SmartTravelPlanner.Api.Routing;

using SmartTravelPlanner.Api.Classification;

public interface IToolRouter
{
    ToolRouteDecision Route(ExecutionStep step);
}

public sealed record ToolRouteDecision(
    bool IsMandatory,
    string? ToolName,
    IReadOnlyDictionary<string, object?> Arguments,
    string Reason,
    int? StepOrder = null)
{
    public static ToolRouteDecision ModelSelected(string reason = "No mandatory intent was detected.") =>
        new(false, null, new Dictionary<string, object?>(), reason);
}
