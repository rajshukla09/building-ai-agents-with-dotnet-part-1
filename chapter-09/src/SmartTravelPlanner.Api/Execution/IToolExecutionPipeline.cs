using SmartTravelPlanner.Api.Routing;

namespace SmartTravelPlanner.Api.Execution;

public interface IToolExecutionPipeline
{
    Task<object?> ExecuteAsync(ToolRouteDecision decision, CancellationToken cancellationToken = default);
    T ExecuteModelSelected<T>(string toolName, object input, Func<T> operation);
}
