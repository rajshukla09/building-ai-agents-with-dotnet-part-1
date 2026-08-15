namespace SmartTravelPlanner.Api.Classification;

public sealed class ExecutionPlanProvider(IRequestClassifier classifier) : IExecutionPlanProvider
{
    public Task<ExecutionPlan> CreateAsync(string request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        return classifier.ClassifyAsync(request, cancellationToken);
    }
}
