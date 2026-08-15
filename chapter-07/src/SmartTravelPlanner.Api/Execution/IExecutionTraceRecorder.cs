using SmartTravelPlanner.Api.Models.Execution;

namespace SmartTravelPlanner.Api.Execution;

public interface IExecutionTraceRecorder
{
    ExecutionTraceScope BeginRequest();

    T RecordToolCall<T>(string toolName, object? input, Func<T> operation);
}
