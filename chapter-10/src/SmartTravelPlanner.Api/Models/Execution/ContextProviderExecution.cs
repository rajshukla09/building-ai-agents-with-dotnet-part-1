namespace SmartTravelPlanner.Api.Models.Execution;

public sealed record ContextProviderExecution
{
    public required int Order
    {
        get; init;
    }
    public required string ProviderName
    {
        get; init;
    }
    public required string ContextCategory
    {
        get; init;
    }
    public required long DurationMs
    {
        get; init;
    }
    public required bool ContextAdded
    {
        get; init;
    }
    public required string Status
    {
        get; init;
    }
}
