namespace SmartTravelPlanner.Api.Classification;

/// <summary>
/// Typed, serializable request envelope that can be passed directly to a future workflow.
/// </summary>
public sealed record RequestClassification
{
    public required RequestIntent Intent { get; init; }
    public string? Origin { get; init; }
    public string? Destination { get; init; }
    public decimal? Amount { get; init; }
    public string? FromCurrency { get; init; }
    public string? ToCurrency { get; init; }
    public string? City { get; init; }
    public double? Confidence { get; init; }
}
