using SmartTravelPlanner.Api.Models.Execution;

namespace SmartTravelPlanner.Api.Execution;

public interface IExecutionTraceRecorder
{
    ExecutionTraceScope BeginRequest();

    void RecordToolExecution(string toolName, ToolInvocationMode invocationMode, int? planStepOrder,
        DateTimeOffset startedAt, DateTimeOffset completedAt, int retryCount,
        string status, bool timeout, object? input, object? output, string? failureReason);

    void RecordExecutionPlanValidation(DateTimeOffset startedAt, DateTimeOffset completedAt,
        SmartTravelPlanner.Api.Classification.ExecutionPlan initialPlan,
        SmartTravelPlanner.Api.Classification.ExecutionPlanValidationResult initialValidation,
        bool repairAttempted,
        SmartTravelPlanner.Api.Classification.ExecutionPlan? repairedPlan,
        SmartTravelPlanner.Api.Classification.ExecutionPlanValidationResult finalValidation);

    void RecordPlanExecutionDuration(long durationMs);

    ValueTask<T> RecordContextProviderAsync<T>(string providerName, string category, Func<ValueTask<T>> operation, Func<T, bool> contextAdded);

    T RecordContextProvider<T>(string providerName, string category, Func<T> operation, Func<T, bool> contextAdded);
}
