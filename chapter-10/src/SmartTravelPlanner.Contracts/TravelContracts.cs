using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace SmartTravelPlanner.Contracts;

public enum RequestIntent
{
    TravelPlanning,
    DistanceLookup,
    CurrencyConversion,
    LocalTime,
    WeatherLookup,
    Unknown
}

public enum WorkflowLiveEventType
{
    WorkflowStarted,
    ExecutorWaiting,
    ExecutorStarted,
    ExecutorCompleted,
    ExecutorFailed,
    AgentStarted,
    AgentStructuredResponseReceived,
    AgentDeserializationFailed,
    AgentValidationFailed,
    AgentRecoveryStarted,
    AgentRecoveryCompleted,
    AgentRegenerationStarted,
    AgentRegenerationCompleted,
    AgentStreaming,
    AgentCompleted,
    AgentFailed,
    ToolStarted,
    ToolRetried,
    ToolCompleted,
    ToolFailed,
    ContextProviderStarted,
    ContextProviderCompleted,
    MessageProduced,
    WorkflowCompleted,
    WorkflowFailed,
    WorkflowCancelled
}

public enum WorkflowStageType
{
    Workflow,
    Executor,
    Agent,
    Tool,
    ContextProvider,
    Message
}

public enum WorkflowStageStatus
{
    Waiting,
    Running,
    Completed,
    Failed,
    Cancelled,
    Skipped
}

public enum WorkflowNodeType
{
    Executor,
    Agent,
    Tool,
    ContextProvider
}

public sealed record TravelPlanRequest([Required, StringLength(100)] string Destination,
                                       [Range(1, 14)] int DurationDays, [StringLength(500)] string? Preferences = null)
{
    public const int MaximumDurationDays = 14;
}

public sealed record TripPlanResponse(TripPlan TripPlan, ExecutionTrace Execution,
                                      WorkflowExecutionTrace? Workflow = null);
public sealed record TripPlan(string Destination, int DurationDays, string Summary, IReadOnlyList<TripDay> Days);
public sealed record TripDay(int DayNumber, string Title, IReadOnlyList<TripActivity> Activities);
public sealed record TripActivity(string Time, string Name, string Description, string Category, string Notes);
public sealed record ExecutionTrace(DateTimeOffset StartedAt, DateTimeOffset CompletedAt, long TotalDurationMs,
                                    IReadOnlyList<ToolExecution> ToolCalls,
                                    IReadOnlyList<ContextProviderExecution> ContextProviders,
                                    ExecutionPlanTrace? ExecutionPlan);
public sealed record ToolExecution(int Order, int? PlanStepOrder, string ToolName, string InvocationMode,
                                   DateTimeOffset StartedAt, DateTimeOffset CompletedAt, long DurationMs, string Status,
                                   int RetryCount, bool Timeout, JsonElement? Input, JsonElement? Output,
                                   string? Error);
public sealed record ContextProviderExecution(int Order, string ProviderName, string ContextCategory, long DurationMs,
                                              bool ContextAdded, string Status);
public sealed record ExecutionPlanTrace(DateTimeOffset ClassificationStartedAt,
                                        DateTimeOffset ClassificationCompletedAt, long ClassificationDurationMs,
                                        ExecutionPlanDto? InitialPlan, DateTimeOffset? ValidationStartedAt,
                                        DateTimeOffset? ValidationCompletedAt, long? ValidationDurationMs,
                                        bool InitialValidationSucceeded, IReadOnlyList<string> InitialValidationErrors,
                                        bool RepairAttempted, DateTimeOffset? RepairStartedAt,
                                        DateTimeOffset? RepairCompletedAt, long? RepairDurationMs,
                                        ExecutionPlanDto? RepairedPlan, bool FinalValidationSucceeded,
                                        IReadOnlyList<string> FinalValidationErrors, long? ToolExecutionDurationMs);
public sealed record ExecutionPlanDto(RequestIntent Intent, IReadOnlyList<ExecutionStepDto> Steps);
public sealed record ExecutionStepDto(int Order, JsonElement Tool, IReadOnlyDictionary<string, JsonElement> Arguments);
public sealed record WorkflowExecutionTrace(Guid WorkflowRunId, DateTimeOffset StartedAt, DateTimeOffset CompletedAt,
                                            long DurationMs, string Status, string? Exception,
                                            IReadOnlyList<ExecutorExecutionTrace> Executors,
                                            IReadOnlyList<WorkflowMessageTransitionDto>? MessageTransitions = null);
public sealed record ExecutorExecutionTrace(Guid WorkflowRunId, string ExecutorName, string MessageType,
                                            DateTimeOffset StartedAt, DateTimeOffset CompletedAt, long DurationMs,
                                            string Status, string? Exception, string? OutputMessageType = null);
public sealed record WorkflowValueChangeDto(object? Before, object? After);
public sealed record WorkflowMessageTransitionDto(int Order, string ExecutorName, string InputMessageType,
                                                  string OutputMessageType,
                                                  IReadOnlyDictionary<string, object?> CarriedForward,
                                                  IReadOnlyDictionary<string, object?> Added,
                                                  IReadOnlyDictionary<string, WorkflowValueChangeDto> Changed,
                                                  object? InputSnapshot, object? OutputSnapshot);
public sealed record ApiProblem(string? Title, string? Detail, IReadOnlyDictionary<string, string[]>? Errors);

public sealed record StartWorkflowRunResponse(Guid WorkflowRunId, string Status);
public sealed record WorkflowLiveEventDto(Guid WorkflowRunId, long Sequence, DateTimeOffset OccurredAt,
                                          WorkflowLiveEventType EventType, WorkflowStageType StageType,
                                          string StageName, WorkflowStageStatus Status, string? InputMessageType,
                                          string? OutputMessageType, long? DurationMs, string? Summary, object? Data);
public sealed record WorkflowTopologyDto(string WorkflowName, IReadOnlyList<WorkflowNodeDto> Nodes,
                                         IReadOnlyList<WorkflowEdgeDto> Edges);
public sealed record WorkflowNodeDto(string Id, string Name, WorkflowNodeType NodeType, string InputMessageType,
                                     string OutputMessageType, int Order, string Uses);
public sealed record WorkflowEdgeDto(string FromNodeId, string ToNodeId);

public sealed record WorkflowRunQuery(string? Status = null, string? Destination = null, bool? RepairAttempted = null,
                                      DateTimeOffset? From = null, DateTimeOffset? To = null, int Page = 1,
                                      int PageSize = 20);
public sealed record WorkflowRunSummaryDto(Guid WorkflowRunId, string Destination, string Status,
                                           DateTimeOffset StartedAt, long DurationMs, bool RepairAttempted,
                                           int ExecutorCount, int ToolCount);
public sealed record PagedWorkflowRunsDto(IReadOnlyList<WorkflowRunSummaryDto> Items, int Page, int PageSize,
                                          int TotalCount);
public sealed record PersistedExecutorDto(int Order, string ExecutorName, string InputMessageType,
                                          string OutputMessageType, DateTimeOffset StartedAt,
                                          DateTimeOffset CompletedAt, long DurationMs, string Status,
                                          string? Exception);
public sealed record PersistedToolDto(int Order, int? PlanStepOrder, string ToolName, string InvocationMode,
                                      string? InputJson, string? OutputJson, DateTimeOffset StartedAt,
                                      DateTimeOffset CompletedAt, long DurationMs, string Status, int RetryCount,
                                      bool Timeout, string? Error, string? FailureReason);
public sealed record PersistedContextProviderDto(int Order, string ProviderName, string ContextCategory,
                                                 long DurationMs, bool ContextAdded, string Status,
                                                 string? SafeContextSummary);
public sealed record PersistedExecutionPlanDto(string? InitialPlanJson, bool InitialValidationSucceeded,
                                               string InitialValidationErrorsJson, bool RepairAttempted,
                                               string? RepairedPlanJson, bool FinalValidationSucceeded,
                                               string FinalValidationErrorsJson, string? GeneratedPlanJson);
public sealed record WorkflowRunDetailsDto(
    Guid WorkflowRunId, string Destination, int DurationDays, string OriginalRequest, string Status,
    DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, long DurationMs, bool RepairAttempted, string? FailureStage,
    string? Error, string? TripPlanJson, IReadOnlyList<PersistedExecutorDto> Executors,
    IReadOnlyList<WorkflowMessageTransitionDto> MessageTransitions, IReadOnlyList<PersistedToolDto> Tools,
    IReadOnlyList<PersistedContextProviderDto> ContextProviders, PersistedExecutionPlanDto? ExecutionPlan);
public sealed record NamedDurationDifferenceDto(string Name, long? RunAMs, long? RunBMs, long DifferenceMs);
public sealed record WorkflowRunComparisonDto(WorkflowRunSummaryDto RunA, WorkflowRunSummaryDto RunB,
                                              long TotalDurationDifferenceMs,
                                              IReadOnlyList<NamedDurationDifferenceDto> ExecutorDurationDifferences,
                                              IReadOnlyList<string> ToolCallDifferences, bool RepairDiffers,
                                              IReadOnlyList<string> ExecutionPlanDifferences,
                                              IReadOnlyList<string> ContextProviderDifferences, bool StatusDiffers,
                                              bool FinalResultSummaryDiffers);
