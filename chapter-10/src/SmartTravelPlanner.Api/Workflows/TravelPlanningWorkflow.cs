using Microsoft.Agents.AI.Workflows;
using SmartTravelPlanner.Api.Workflows.Executors;

namespace SmartTravelPlanner.Api.Workflows;

/// <summary>Defines the chapter's deliberately linear workflow topology.</summary>
public sealed class TravelPlanningWorkflow(
    ExecutionPlanExecutor planning,
    ExecutionPlanValidationExecutor validation,
    ToolExecutionExecutor tools,
    TravelAgentExecutor travelAgent)
{
    public Workflow Create() => new WorkflowBuilder(planning)
        .AddEdge(planning, validation)
        .AddEdge(validation, tools)
        .AddEdge(tools, travelAgent)
        .WithOutputFrom(travelAgent)
        .Build();
}
