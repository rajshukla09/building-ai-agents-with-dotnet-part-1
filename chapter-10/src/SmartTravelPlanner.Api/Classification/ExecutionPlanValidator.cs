using System.Globalization;
using System.Text.Json;

namespace SmartTravelPlanner.Api.Classification;

public sealed class ExecutionPlanValidator
{
    public ExecutionPlanValidationResult Validate(ExecutionPlan? plan)
    {
        List<string> errors = [];
        if (plan is null)
        {
            errors.Add("The request classifier returned no execution plan.");
            return Result(errors);
        }

        ToolType? requiredTool = plan.Intent switch
        {
            RequestIntent.DistanceLookup => ToolType.Distance,
            RequestIntent.CurrencyConversion => ToolType.Currency,
            RequestIntent.LocalTime => ToolType.LocalTime,
            RequestIntent.WeatherLookup => ToolType.Weather,
            _ => null
        };
        if (requiredTool.HasValue && !plan.Steps.Any(step => step.Tool == requiredTool.Value))
            errors.Add($"{plan.Intent} requires a {requiredTool} execution step.");

        for (int index = 0; index < plan.Steps.Count; index++)
        {
            ExecutionStep step = plan.Steps[index];
            if (step.Order != index + 1)
                errors.Add($"Step at index {index} must have order {index + 1}.");
            switch (step.Tool)
            {
                case ToolType.Weather:
                    RequireText(step, "destination", errors);
                    break;

                case ToolType.Distance:
                    RequireText(step, "origin", errors);
                    RequireText(step, "destination", errors);
                    break;

                case ToolType.Currency:
                    RequireAmount(step, errors);
                    RequireText(step, "from", errors);
                    RequireText(step, "to", errors);
                    break;

                case ToolType.LocalTime:
                    RequireText(step, "city", errors);
                    break;
            }
        }
        return Result(errors);
    }

    public static bool TryGetArgument(ExecutionStep step, string name, out object? value)
    {
        ArgumentNullException.ThrowIfNull(step);
        KeyValuePair<string, object?> match = step.Arguments.FirstOrDefault(
            pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase));
        value = match.Value;
        return match.Key is not null;
    }

    public static bool TryGetTextArgument(ExecutionStep step, string name, out string value)
    {
        value = string.Empty;
        if (!TryGetArgument(step, name, out object? raw) || raw is null)
            return false;
        value = raw is JsonElement json
            ? json.ValueKind == JsonValueKind.String ? json.GetString() ?? string.Empty : json.ToString()
            : Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    public static bool TryGetNonNegativeDecimalArgument(ExecutionStep step, string name, out decimal amount)
    {
        amount = default;
        if (!TryGetArgument(step, name, out object? raw) || raw is null)
            return false;
        if (raw is JsonElement json && json.ValueKind == JsonValueKind.Number)
            return json.TryGetDecimal(out amount) && amount >= 0;
        return decimal.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Number,
            CultureInfo.InvariantCulture, out amount) && amount >= 0;
    }

    private static void RequireText(ExecutionStep step, string name, List<string> errors)
    {
        if (!TryGetTextArgument(step, name, out _))
            errors.Add($"Step {step.Order} ({step.Tool}) requires argument '{name}'.");
    }

    private static void RequireAmount(ExecutionStep step, List<string> errors)
    {
        if (!TryGetNonNegativeDecimalArgument(step, "amount", out _))
            errors.Add($"Step {step.Order} (Currency) requires a non-negative numeric 'amount'.");
    }

    private static ExecutionPlanValidationResult Result(List<string> errors) =>
        new()
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
}
