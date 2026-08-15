using SmartTravelPlanner.Api.Classification;
using SmartTravelPlanner.Api.Workflows.Messages;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class WorkflowMessageTests
{
    [Fact]
    public void TypedMessagesCarryAccumulatedContextForward()
    {
        Guid runId = Guid.NewGuid();
        TravelPlanRequest request = new("Jaipur", 2, "Local food");
        ExecutionPlan plan = new()
        {
            Intent = RequestIntent.TravelPlanning
        };
        ExecutionPlanValidationResult valid = new()
        {
            IsValid = true,
            Errors = []
        };

        ToolExecutionMessage message = new(runId, request, "Plan my trip", plan, valid, false, []);

        Assert.Equal(runId, message.WorkflowRunId);
        Assert.Same(request, message.Request);
        Assert.Same(plan, message.ExecutionPlan);
        Assert.Same(valid, message.Validation);
        Assert.Empty(message.ToolResults);
    }
}
