using SmartTravelPlanner.Api.Models.Execution;

namespace SmartTravelPlanner.Api.Execution;

public sealed class ToolExecutionTraceRecorder(TimeProvider timeProvider) : IExecutionTraceRecorder
{
    private readonly AsyncLocal<RequestTrace?> _current = new();

    public ExecutionTraceScope BeginRequest()
    {
        RequestTrace? parent = _current.Value;
        RequestTrace request = new(timeProvider, timeProvider.GetUtcNow());
        _current.Value = request;
        return new ExecutionTraceScope(this, request, parent);
    }

    public T RecordToolCall<T>(string toolName, object? input, Func<T> operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(operation);

        RequestTrace? request = _current.Value;
        if (request is null)
        {
            // Tools remain usable in isolation; only agent requests create execution traces.
            return operation();
        }

        int order = request.NextOrder();
        DateTimeOffset startedAt = timeProvider.GetUtcNow();
        try
        {
            T output = operation();
            DateTimeOffset completedAt = timeProvider.GetUtcNow();
            request.Add(CreateExecution(order, toolName, startedAt, completedAt, "Success", input, output, null));
            return output;
        }
        catch (Exception exception)
        {
            DateTimeOffset completedAt = timeProvider.GetUtcNow();
            request.Add(CreateExecution(
                order,
                toolName,
                startedAt,
                completedAt,
                "Failure",
                input,
                null,
                exception.Message));
            throw;
        }
    }

    internal void EndRequest(ExecutionTraceScope scope, RequestTrace? parent)
    {
        if (ReferenceEquals(_current.Value, scope.Request))
        {
            _current.Value = parent;
        }
    }

    private static ToolExecution CreateExecution(
        int order,
        string toolName,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        string status,
        object? input,
        object? output,
        string? error) => new()
        {
            Order = order,
            ToolName = toolName,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            DurationMs = Math.Max(0, (long)(completedAt - startedAt).TotalMilliseconds),
            Status = status,
            Input = input,
            Output = output,
            Error = error
        };

    internal sealed class RequestTrace(TimeProvider timeProvider, DateTimeOffset startedAt)
    {
        private readonly List<ToolExecution> _toolCalls = [];
        private int _nextOrder;

        public int NextOrder() => Interlocked.Increment(ref _nextOrder);

        public void Add(ToolExecution execution)
        {
            lock (_toolCalls)
            {
                _toolCalls.Add(execution);
            }
        }

        public ExecutionTrace Complete()
        {
            DateTimeOffset completedAt = timeProvider.GetUtcNow();
            ToolExecution[] toolCalls;
            lock (_toolCalls)
            {
                toolCalls = _toolCalls.OrderBy(call => call.Order).ToArray();
            }

            return new ExecutionTrace
            {
                StartedAt = startedAt,
                CompletedAt = completedAt,
                TotalDurationMs = Math.Max(0, (long)(completedAt - startedAt).TotalMilliseconds),
                ToolCalls = toolCalls
            };
        }
    }
}
