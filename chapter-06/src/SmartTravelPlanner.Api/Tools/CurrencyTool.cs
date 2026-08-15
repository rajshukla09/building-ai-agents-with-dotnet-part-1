using System.ComponentModel;
using SmartTravelPlanner.Api.Execution;

namespace SmartTravelPlanner.Api.Tools;

public sealed class CurrencyTool(IExecutionTraceRecorder traceRecorder)
{
    // Rates are expressed as units of the currency for one USD.
    private static readonly IReadOnlyDictionary<string, decimal> UsdRates =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = 1m,
            ["INR"] = 83.50m,
            ["EUR"] = 0.92m,
            ["SAR"] = 3.75m
        };

    [Description("Converts a monetary amount using fixed sample exchange rates. Use for every currency conversion or cross-currency budget question.")]
    public CurrencyConversionResult ConvertCurrency(
        [Description("Three-letter source currency code, for example USD.")] string from,
        [Description("Three-letter target currency code, for example INR.")] string to,
        [Description("Non-negative amount to convert.")] decimal amount)
    {
        return traceRecorder.RecordToolCall(
            nameof(CurrencyTool),
            new { from, to, amount },
            () =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(from);
                ArgumentException.ThrowIfNullOrWhiteSpace(to);
                ArgumentOutOfRangeException.ThrowIfNegative(amount);

                string source = from.Trim().ToUpperInvariant();
                string target = to.Trim().ToUpperInvariant();
                if (!UsdRates.TryGetValue(source, out decimal sourceRate) ||
                    !UsdRates.TryGetValue(target, out decimal targetRate))
                {
                    throw new ArgumentException("Supported currencies are USD, INR, EUR, and SAR.");
                }

                decimal rate = targetRate / sourceRate;
                return new CurrencyConversionResult(
                    source,
                    target,
                    amount,
                    decimal.Round(rate, 6),
                    decimal.Round(amount * rate, 2));
            });
    }
}

public sealed record CurrencyConversionResult(
    string From,
    string To,
    decimal Amount,
    decimal ExchangeRate,
    decimal ConvertedAmount);
