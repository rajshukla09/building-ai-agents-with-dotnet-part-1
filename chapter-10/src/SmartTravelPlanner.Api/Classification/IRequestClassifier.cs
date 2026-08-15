namespace SmartTravelPlanner.Api.Classification;

public interface IRequestClassifier
{
    Task<ExecutionPlan> ClassifyAsync(
        string request,
        CancellationToken cancellationToken = default);
}
