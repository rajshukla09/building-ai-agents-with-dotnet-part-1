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

    public void RecordToolExecution(string toolName, ToolInvocationMode invocationMode, int? planStepOrder,
        DateTimeOffset startedAt, DateTimeOffset completedAt, int retryCount,
        string status, bool timeout, object? input, object? output, string? failureReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        RequestTrace? request = _current.Value;
        if (request is null) return;
        request.Add(new ToolExecution {
            Order = request.NextOrder(), PlanStepOrder = planStepOrder,
            ToolName = toolName, InvocationMode = invocationMode.ToString(),
            StartedAt = startedAt, CompletedAt = completedAt,
            DurationMs = Math.Max(0, (long)(completedAt - startedAt).TotalMilliseconds),
            RetryCount = retryCount, Status = status, Timeout = timeout,
            Input = input, Output = output, Error = failureReason
        });
    }

    public void RecordExecutionPlanValidation(DateTimeOffset startedAt, DateTimeOffset completedAt,
        SmartTravelPlanner.Api.Classification.ExecutionPlan initialPlan,
        SmartTravelPlanner.Api.Classification.ExecutionPlanValidationResult initialValidation,
        bool repairAttempted,
        SmartTravelPlanner.Api.Classification.ExecutionPlan? repairedPlan,
        SmartTravelPlanner.Api.Classification.ExecutionPlanValidationResult finalValidation)
    {
        RequestTrace? request = _current.Value;
        if (request is null) return;
        request.SetExecutionPlan(new ExecutionPlanTrace
        {
            ClassificationStartedAt = startedAt,
            ClassificationCompletedAt = completedAt,
            ClassificationDurationMs = Math.Max(0, (long)(completedAt - startedAt).TotalMilliseconds),
            InitialPlan = initialPlan,
            InitialValidationSucceeded = initialValidation.IsValid,
            InitialValidationErrors = initialValidation.Errors,
            RepairAttempted = repairAttempted,
            RepairedPlan = repairedPlan,
            FinalValidationSucceeded = finalValidation.IsValid,
            FinalValidationErrors = finalValidation.Errors
        });
    }

    public void RecordPlanExecutionDuration(long durationMs) =>
        _current.Value?.SetPlanExecutionDuration(Math.Max(0, durationMs));

    public async ValueTask<T> RecordContextProviderAsync<T>(
        string providerName, string category, Func<ValueTask<T>> operation, Func<T, bool> contextAdded)
    {
        RequestTrace? request = _current.Value;
        if (request is null) return await operation();
        int order = request.NextProviderOrder();
        DateTimeOffset startedAt = timeProvider.GetUtcNow();
        try
        {
            T result = await operation();
            request.AddProvider(new ContextProviderExecution
            {
                Order = order, ProviderName = providerName, ContextCategory = category,
                DurationMs = Duration(startedAt), ContextAdded = contextAdded(result), Status = "Success"
            });
            return result;
        }
        catch
        {
            request.AddProvider(new ContextProviderExecution
            {
                Order = order, ProviderName = providerName, ContextCategory = category,
                DurationMs = Duration(startedAt), ContextAdded = false, Status = "Failure"
            });
            throw;
        }
    }

    public T RecordContextProvider<T>(
        string providerName, string category, Func<T> operation, Func<T, bool> contextAdded)
    {
        RequestTrace? request = _current.Value;
        if (request is null)
        {
            return operation();
        }

        int order = request.NextProviderOrder();
        DateTimeOffset startedAt = timeProvider.GetUtcNow();
        try
        {
            T result = operation();
            request.AddProvider(new ContextProviderExecution
            {
                Order = order,
                ProviderName = providerName,
                ContextCategory = category,
                DurationMs = Duration(startedAt),
                ContextAdded = contextAdded(result),
                Status = "Success"
            });
            return result;
        }
        catch
        {
            request.AddProvider(new ContextProviderExecution
            {
                Order = order,
                ProviderName = providerName,
                ContextCategory = category,
                DurationMs = Duration(startedAt),
                ContextAdded = false,
                Status = "Failure"
            });
            throw;
        }
    }

    private long Duration(DateTimeOffset startedAt) =>
        Math.Max(0, (long)(timeProvider.GetUtcNow() - startedAt).TotalMilliseconds);

    internal void EndRequest(ExecutionTraceScope scope, RequestTrace? parent)
    {
        if (ReferenceEquals(_current.Value, scope.Request))
        {
            _current.Value = parent;
        }
    }

    internal sealed class RequestTrace(TimeProvider timeProvider, DateTimeOffset startedAt)
    {
        private readonly List<ToolExecution> _toolCalls = [];
        private readonly List<ContextProviderExecution> _contextProviders = [];
        private ExecutionPlanTrace? _executionPlan;
        private int _nextOrder;
        private int _nextProviderOrder;

        public int NextOrder() => Interlocked.Increment(ref _nextOrder);
        public int NextProviderOrder() => Interlocked.Increment(ref _nextProviderOrder);

        public void Add(ToolExecution execution)
        {
            lock (_toolCalls)
            {
                _toolCalls.Add(execution);
            }
        }

        public void AddProvider(ContextProviderExecution execution)
        {
            lock (_contextProviders) _contextProviders.Add(execution);
        }

        public void SetExecutionPlan(ExecutionPlanTrace executionPlan) => _executionPlan = executionPlan;

        public void SetPlanExecutionDuration(long durationMs)
        {
            if (_executionPlan is not null) _executionPlan = _executionPlan with { PlanExecutionDurationMs = durationMs };
        }

        public ExecutionTrace Complete()
        {
            DateTimeOffset completedAt = timeProvider.GetUtcNow();
            ToolExecution[] toolCalls;
            lock (_toolCalls)
            {
                toolCalls = _toolCalls.OrderBy(call => call.Order).ToArray();
            }
            ContextProviderExecution[] providers;
            lock (_contextProviders)
            {
                providers = _contextProviders.OrderBy(provider => provider.Order).ToArray();
            }

            return new ExecutionTrace
            {
                StartedAt = startedAt,
                CompletedAt = completedAt,
                TotalDurationMs = Math.Max(0, (long)(completedAt - startedAt).TotalMilliseconds),
                ToolCalls = toolCalls,
                ContextProviders = providers,
                ExecutionPlan = _executionPlan
            };
        }
    }
}
