using SmartTravelPlanner.Api.Contracts;
using SmartTravelPlanner.Api.Models.Memory;

namespace SmartTravelPlanner.Api.Memory;

public sealed class TravelerMemoryService(ITravelerMemoryStore store, TimeProvider timeProvider)
{
    public TravelerMemory? Get(Guid travelerId) => store.Get(travelerId);

    public TravelerMemory Save(Guid travelerId, TravelerMemoryRequest request)
    {
        if (travelerId == Guid.Empty)
            throw new ArgumentException("TravelerId cannot be empty.", nameof(travelerId));
        return store.Upsert(new TravelerMemory
        {
            TravelerId = travelerId,
            FoodPreferences = Clean(request.FoodPreferences),
            ActivityInterests = Clean(request.ActivityInterests),
            TravelPace = Clean(request.TravelPace),
            BudgetPreference = Clean(request.BudgetPreference),
            AccessibilityRequirements = Clean(request.AccessibilityRequirements),
            UpdatedAt = timeProvider.GetUtcNow()
        });
    }

    public bool Delete(Guid travelerId) => store.Delete(travelerId);

    public string BuildContext(Guid travelerId)
    {
        TravelerMemory? memory = Get(travelerId);
        if (memory is null)
            return string.Empty;
        return $"""
            Durable traveller preferences (apply when relevant; this is not conversation history):
            - Food: {Join(memory.FoodPreferences)}
            - Activity interests: {Join(memory.ActivityInterests)}
            - Travel pace: {memory.TravelPace ?? "not specified"}
            - Budget: {memory.BudgetPreference ?? "not specified"}
            - Accessibility: {Join(memory.AccessibilityRequirements)}
            """;
    }

    private static IReadOnlyCollection<string> Clean(IReadOnlyCollection<string>? values) => values?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Join(IReadOnlyCollection<string> values) => values.Count == 0 ? "not specified" : string.Join(", ", values);
}
