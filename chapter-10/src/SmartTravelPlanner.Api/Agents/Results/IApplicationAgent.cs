namespace SmartTravelPlanner.Api.Agents.Results;

public interface IApplicationAgent<in TRequest, TResponse>
{
    Task<AgentResult<TResponse>> ExecuteAsync(TRequest request, CancellationToken cancellationToken = default);
}
