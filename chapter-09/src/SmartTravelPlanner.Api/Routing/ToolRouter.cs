using SmartTravelPlanner.Api.Classification;

namespace SmartTravelPlanner.Api.Routing;

/// <summary>Maps a validated classification to execution. It never parses user text.</summary>
public sealed class ToolRouter(ILogger<ToolRouter> logger) : IToolRouter
{
    public ToolRouteDecision Route(ExecutionStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        ToolRouteDecision decision = step.Tool switch
        {
            ToolType.Distance => Mandatory("DistanceTool", new Dictionary<string, object?>
            {
                ["origin"] = Text(step, "origin"),
                ["destination"] = Text(step, "destination")
            }, step),
            ToolType.Currency => Mandatory("CurrencyTool", new Dictionary<string, object?>
            {
                ["amount"] = Amount(step),
                ["from"] = Text(step, "from"),
                ["to"] = Text(step, "to")
            }, step),
            ToolType.LocalTime => Mandatory("TimeZoneTool", new Dictionary<string, object?>
            {
                ["city"] = Text(step, "city")
            }, step),
            ToolType.Weather => Mandatory("WeatherTool", new Dictionary<string, object?>
            {
                ["destination"] = Text(step, "destination")
            }, step),
            _ => throw new ArgumentOutOfRangeException(nameof(step), step.Tool, "Unsupported tool type.")
        };
        logger.LogInformation("Routing execution step {StepOrder}: {Tool}", step.Order, decision.ToolName);
        return decision;
    }

    private static ToolRouteDecision Mandatory(
        string toolName, IReadOnlyDictionary<string, object?> arguments, ExecutionStep step) =>
        new(true, toolName, arguments, $"Step {step.Order} requires deterministic execution.", step.Order);

    private static string Text(ExecutionStep step, string key)
    {
        return ExecutionPlanValidator.TryGetTextArgument(step, key, out string value)
            ? value
            : throw new RequestClassificationException($"Step {step.Order}: Argument '{key}' is invalid.");
    }

    private static decimal Amount(ExecutionStep step, string key = "amount") =>
        ExecutionPlanValidator.TryGetNonNegativeDecimalArgument(step, key, out decimal amount)
            ? amount
            : throw new RequestClassificationException($"Step {step.Order}: Currency amount is invalid.");
}
