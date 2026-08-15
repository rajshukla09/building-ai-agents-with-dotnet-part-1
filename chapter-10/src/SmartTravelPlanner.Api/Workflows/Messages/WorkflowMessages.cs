using SmartTravelPlanner.Api.Classification;

namespace SmartTravelPlanner.Api.Workflows.Messages;

public sealed record TravelWorkflowRequest(
    Guid WorkflowRunId,
    TravelPlanRequest Request,
    string OriginalUserRequest);

public sealed record ExecutionPlanMessage(
    Guid WorkflowRunId,
    TravelPlanRequest Request,
    string OriginalUserRequest,
    ExecutionPlan ExecutionPlan);

public sealed record ValidatedPlanMessage(
    Guid WorkflowRunId,
    TravelPlanRequest Request,
    string OriginalUserRequest,
    ExecutionPlan ExecutionPlan,
    ExecutionPlanValidationResult InitialValidation,
    ExecutionPlanValidationResult FinalValidation,
    bool Repaired);

public sealed record ToolExecutionMessage(
    Guid WorkflowRunId,
    TravelPlanRequest Request,
    string OriginalUserRequest,
    ExecutionPlan ExecutionPlan,
    ExecutionPlanValidationResult Validation,
    bool Repaired,
    IReadOnlyList<ToolStepResult> ToolResults);

public sealed record ToolStepResult(
    int Order,
    ToolType Tool,
    string Status,
    object? Output,
    string? FailureReason);
