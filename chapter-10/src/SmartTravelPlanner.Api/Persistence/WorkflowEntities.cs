namespace SmartTravelPlanner.Api.Persistence;

public sealed class WorkflowRunEntity
{
    public long Id { get; set; }

    public Guid WorkflowRunId { get; set; }

    public string Destination { get; set; } = "";
    public int DurationDays { get; set; }

    public string OriginalRequest { get; set; } = "";
    public string Status { get; set; } = "Running";
    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public long DurationMs { get; set; }

    public bool RepairAttempted { get; set; }

    public string? FailureStage { get; set; }

    public string? Error { get; set; }

    public string? TripPlanJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<WorkflowExecutorTraceEntity> Executors { get; set; } = [];
    public List<WorkflowAgentExecutionEntity> AgentExecutions { get; set; } = [];
    public List<WorkflowLiveEventEntity> LiveEvents { get; set; } = [];
    public List<WorkflowMessageTransitionEntity> Transitions { get; set; } = [];
    public List<ToolExecutionTraceEntity> Tools { get; set; } = [];
    public List<ContextProviderTraceEntity> ContextProviders { get; set; } = [];
    public ExecutionPlanSnapshotEntity? ExecutionPlan { get; set; }
}

public sealed class WorkflowAgentExecutionEntity
{
    public long Id { get; set; }
    public Guid WorkflowRunId { get; set; }
    public string ExecutorName { get; set; } = "";
    public string AgentName { get; set; } = "";
    public string RequestedResponseType { get; set; } = "";
    public bool IsSuccess { get; set; }
    public string? FailureKind { get; set; }
    public string? FailureCode { get; set; }
    public string? FailurePath { get; set; }
    public bool Retryable { get; set; }
    public int AttemptCount { get; set; }
    public bool StructuredDeserializationSucceeded { get; set; }
    public bool RawRecoveryAttempted { get; set; }
    public bool RawRecoverySucceeded { get; set; }
    public bool RegenerationAttempted { get; set; }
    public bool RegenerationSucceeded { get; set; }
    public long DurationMs { get; set; }
    public string FinalStatus { get; set; } = "";
    public string WarningsJson { get; set; } = "[]";
}

public sealed class WorkflowExecutorTraceEntity
{
    public long Id { get; set; }

    public Guid WorkflowRunId { get; set; }

    public int Order { get; set; }

    public string ExecutorName { get; set; } = "";
    public string InputMessageType { get; set; } = "";
    public string OutputMessageType { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset CompletedAt { get; set; }

    public long DurationMs { get; set; }

    public string Status { get; set; } = "";
    public string? Exception { get; set; }
}

public sealed class WorkflowMessageTransitionEntity
{
    public long Id { get; set; }

    public Guid WorkflowRunId { get; set; }

    public int Order { get; set; }

    public string ExecutorName { get; set; } = "";
    public string InputMessageType { get; set; } = "";
    public string OutputMessageType { get; set; } = "";
    public string CarriedForwardJson { get; set; } = "{}";
    public string AddedJson { get; set; } = "{}";
    public string ChangedJson { get; set; } = "{}";
    public string? InputSummaryJson { get; set; }

    public string? OutputSummaryJson { get; set; }
}

public sealed class ToolExecutionTraceEntity
{
    public long Id { get; set; }

    public Guid WorkflowRunId { get; set; }

    public int Order { get; set; }

    public int? PlanStepOrder { get; set; }

    public string ToolName { get; set; } = "";
    public string InvocationMode { get; set; } = "";
    public string? InputJson { get; set; }

    public string? OutputJson { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset CompletedAt { get; set; }

    public long DurationMs { get; set; }

    public string Status { get; set; } = "";
    public int RetryCount { get; set; }

    public bool Timeout { get; set; }

    public string? Error { get; set; }

    public string? FailureReason { get; set; }
}

public sealed class ContextProviderTraceEntity
{
    public long Id { get; set; }

    public Guid WorkflowRunId { get; set; }

    public int Order { get; set; }

    public string ProviderName { get; set; } = "";
    public string ContextCategory { get; set; } = "";
    public long DurationMs { get; set; }

    public bool ContextAdded { get; set; }

    public string Status { get; set; } = "";
    public string? SafeContextSummary { get; set; }
}

public sealed class ExecutionPlanSnapshotEntity
{
    public long Id { get; set; }

    public Guid WorkflowRunId { get; set; }

    public string? InitialPlanJson { get; set; }

    public bool InitialValidationSucceeded { get; set; }

    public string InitialValidationErrorsJson { get; set; } = "[]";
    public bool RepairAttempted { get; set; }

    public string? RepairedPlanJson { get; set; }

    public bool FinalValidationSucceeded { get; set; }

    public string FinalValidationErrorsJson { get; set; } = "[]";
    public string? GeneratedPlanJson { get; set; }
}

public sealed class WorkflowLiveEventEntity
{
    public long Id { get; set; }

    public Guid WorkflowRunId { get; set; }

    public long Sequence { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public string EventType { get; set; } = "";
    public string StageType { get; set; } = "";
    public string StageName { get; set; } = "";
    public string Status { get; set; } = "";

    public string? InputMessageType { get; set; }

    public string? OutputMessageType { get; set; }

    public long? DurationMs { get; set; }

    public string? Summary { get; set; }

    public string? SafeDataJson { get; set; }
}
