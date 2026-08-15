using SmartTravelPlanner.Api.Execution;

namespace SmartTravelPlanner.Api.Classification;

public sealed class ExecutionPlanProvider(
    IRequestClassifier classifier,
    ExecutionPlanValidator validator,
    IExecutionPlanRepairAgent repairAgent,
    IExecutionTraceRecorder traceRecorder,
    TimeProvider timeProvider,
    ILogger<ExecutionPlanProvider> logger) : IExecutionPlanProvider
{
    public async Task<ExecutionPlan> CreateAsync(string request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        DateTimeOffset startedAt = timeProvider.GetUtcNow();
        ExecutionPlan initialPlan = await classifier.ClassifyAsync(request, cancellationToken);
        DateTimeOffset classifiedAt = timeProvider.GetUtcNow();
        ExecutionPlanValidationResult initialValidation = validator.Validate(initialPlan);
        logger.LogInformation("Initial execution plan validation succeeded: {IsValid}; errors: {Errors}",
            initialValidation.IsValid, initialValidation.Errors);

        if (initialValidation.IsValid)
        {
            traceRecorder.RecordExecutionPlanValidation(startedAt, classifiedAt, initialPlan,
                initialValidation, false, null, initialValidation);
            return initialPlan;
        }

        logger.LogWarning("Attempting one execution plan repair for errors: {Errors}", initialValidation.Errors);
        ExecutionPlan repairedPlan;
        try
        {
            repairedPlan = await repairAgent.RepairAsync(
                request, initialPlan, initialValidation.Errors, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ExecutionPlanValidationResult failedRepair = new()
            {
                IsValid = false,
                Errors = [$"Repair agent failed: {exception.Message}"]
            };
            traceRecorder.RecordExecutionPlanValidation(startedAt, classifiedAt, initialPlan,
                initialValidation, true, null, failedRepair);
            throw new RequestClassificationException(
                $"The execution plan could not be repaired: {exception.Message}");
        }
        ExecutionPlanValidationResult finalValidation = validator.Validate(repairedPlan);
        traceRecorder.RecordExecutionPlanValidation(startedAt, classifiedAt, initialPlan,
            initialValidation, true, repairedPlan, finalValidation);
        logger.LogInformation("Final execution plan validation succeeded: {IsValid}; errors: {Errors}",
            finalValidation.IsValid, finalValidation.Errors);

        if (!finalValidation.IsValid)
            throw new RequestClassificationException(
                $"The execution plan remained invalid after one repair attempt: {string.Join("; ", finalValidation.Errors)}");
        return repairedPlan;
    }
}
