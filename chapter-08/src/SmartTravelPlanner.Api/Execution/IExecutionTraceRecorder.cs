using SmartTravelPlanner.Api.Models.Execution;

namespace SmartTravelPlanner.Api.Execution;

public interface IExecutionTraceRecorder
{
    ExecutionTraceScope BeginRequest();

    T RecordToolCall<T>(string toolName, object? input, Func<T> operation);

    ValueTask<T> RecordContextProviderAsync<T>(string providerName, string category, Func<ValueTask<T>> operation, Func<T, bool> contextAdded);

    T RecordContextProvider<T>(string providerName, string category, Func<T> operation, Func<T, bool> contextAdded);
}
