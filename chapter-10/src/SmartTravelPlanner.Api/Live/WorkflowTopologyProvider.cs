using SmartTravelPlanner.Contracts;

namespace SmartTravelPlanner.Api.Live;

public static class WorkflowTopologyProvider
{
    public static WorkflowTopologyDto TravelPlanning() =>
        new("TravelPlanningWorkflow",
            [
                new("execution-plan", "ExecutionPlanExecutor", WorkflowNodeType.Executor, "TravelWorkflowRequest",
                    "ExecutionPlanMessage", 1, "RequestClassifier / execution-plan provider"),
                new("validation", "ExecutionPlanValidationExecutor", WorkflowNodeType.Executor, "ExecutionPlanMessage",
                    "ValidatedPlanMessage", 2, "Validator; may invoke Repair Agent"),
                new("tools", "ToolExecutionExecutor", WorkflowNodeType.Executor, "ValidatedPlanMessage",
                    "ToolExecutionMessage", 3, "Tool Router and Tool Pipeline"),
                new("travel-agent", "TravelAgentExecutor", WorkflowNodeType.Executor, "ToolExecutionMessage",
                    "TripPlanResponse", 4, "Travel Agent")
            ],
            [new("execution-plan", "validation"), new("validation", "tools"), new("tools", "travel-agent")]);
}
