using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SmartTravelPlanner.Api.Execution;
using SmartTravelPlanner.Api.Memory;
using SmartTravelPlanner.Api.Models.Memory;

namespace SmartTravelPlanner.Api.Context;

public sealed class TravelerMemoryContextProvider : AIContextProvider
{
    public TravelerMemoryContextProvider(
        TravelInvocationContextAccessor accessor,
        TravelerMemoryService memoryService,
        IExecutionTraceRecorder traceRecorder,
        ILogger<TravelerMemoryContextProvider> logger)
        : base(messages => AddContext(messages, accessor, memoryService, traceRecorder, logger))
    {
    }

    private static IEnumerable<ChatMessage> AddContext(
        IEnumerable<ChatMessage> messages,
        TravelInvocationContextAccessor accessor,
        TravelerMemoryService memoryService,
        IExecutionTraceRecorder traceRecorder,
        ILogger<TravelerMemoryContextProvider> logger)
    {
        try
        {
            return traceRecorder.RecordContextProvider(
                nameof(TravelerMemoryContextProvider),
                "TravelerMemory",
                () => AppendMemoryContext(messages, accessor, memoryService),
                result => !ReferenceEquals(result, messages));
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Traveler memory context could not be supplied");
            return messages;
        }
    }

    internal static IEnumerable<ChatMessage> AppendMemoryContext(
        IEnumerable<ChatMessage> messages,
        TravelInvocationContextAccessor accessor,
        TravelerMemoryService memoryService)
    {
        Guid? travelerId = accessor.Current?.TravelerId;
        TravelerMemory? memory = travelerId.HasValue ? memoryService.Get(travelerId.Value) : null;
        if (memory is null)
        {
            return messages;
        }

        List<string> preferences = [];
        Add(preferences, "Food", memory.FoodPreferences);
        Add(preferences, "Activity interests", memory.ActivityInterests);
        Add(preferences, "Travel pace", memory.TravelPace);
        Add(preferences, "Budget", memory.BudgetPreference);
        Add(preferences, "Accessibility", memory.AccessibilityRequirements);
        if (preferences.Count == 0)
        {
            return messages;
        }

        string context = "Relevant durable traveler preferences (apply only when relevant):\n" +
                         string.Join("\n", preferences.Select(value => $"- {value}"));
        return messages.Append(new ChatMessage(ChatRole.System, context));
    }

    private static void Add(List<string> values, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add($"{label}: {value}");
        }
    }

    private static void Add(List<string> values, string label, IReadOnlyCollection<string> items)
    {
        if (items.Count > 0)
        {
            values.Add($"{label}: {string.Join(", ", items)}");
        }
    }
}
