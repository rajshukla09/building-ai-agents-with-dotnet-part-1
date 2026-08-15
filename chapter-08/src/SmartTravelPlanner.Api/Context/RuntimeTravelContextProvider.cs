using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SmartTravelPlanner.Api.Execution;

namespace SmartTravelPlanner.Api.Context;

public sealed class RuntimeTravelContextProvider : AIContextProvider
{
    public RuntimeTravelContextProvider(
        TravelInvocationContextAccessor accessor,
        TimeProvider timeProvider,
        IExecutionTraceRecorder traceRecorder,
        ILogger<RuntimeTravelContextProvider> logger)
        : base(messages => AddContext(messages, accessor, timeProvider, traceRecorder, logger))
    {
    }

    private static IEnumerable<ChatMessage> AddContext(
        IEnumerable<ChatMessage> messages,
        TravelInvocationContextAccessor accessor,
        TimeProvider timeProvider,
        IExecutionTraceRecorder traceRecorder,
        ILogger<RuntimeTravelContextProvider> logger)
    {
        try
        {
            return traceRecorder.RecordContextProvider(
                nameof(RuntimeTravelContextProvider),
                "RuntimeTravel",
                () => AppendRuntimeContext(messages, accessor, timeProvider),
                _ => true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Runtime travel context could not be supplied");
            return messages;
        }
    }

    internal static IEnumerable<ChatMessage> AppendRuntimeContext(
        IEnumerable<ChatMessage> messages,
        TravelInvocationContextAccessor accessor,
        TimeProvider timeProvider)
    {
        TravelInvocationContext? current = accessor.Current;
        List<string> values = [$"Current UTC date and time: {timeProvider.GetUtcNow():O}"];
        if (current?.TravelerId is { } travelerId)
        {
            values.Add($"Traveler ID: {travelerId}");
        }
        if (current?.ConversationId is { } conversationId)
        {
            values.Add($"Conversation ID: {conversationId}");
        }
        if (current?.SessionStatus is { } status)
        {
            values.Add($"Session status: {status}");
        }
        if (!string.IsNullOrWhiteSpace(current?.Destination))
        {
            values.Add($"Requested destination: {current.Destination}");
        }
        if (current?.DurationDays is { } duration)
        {
            values.Add($"Requested duration: {duration} days");
        }

        string context = "Invocation-only runtime travel context:\n" + string.Join("\n", values);
        return messages.Append(new ChatMessage(ChatRole.System, context));
    }
}
