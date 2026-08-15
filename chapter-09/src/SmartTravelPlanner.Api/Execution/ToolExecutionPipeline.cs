using Microsoft.Extensions.Options;
using SmartTravelPlanner.Api.Configuration;
using SmartTravelPlanner.Api.Routing;
using SmartTravelPlanner.Api.Tools;

namespace SmartTravelPlanner.Api.Execution;

public sealed class ToolExecutionPipeline(
    IExecutionTraceRecorder traceRecorder,
    IOptions<ToolExecutionOptions> options,
    WeatherTool weather,
    CurrencyTool currency,
    TimeZoneTool timeZone,
    DistanceTool distance,
    TimeProvider timeProvider,
    ILogger<ToolExecutionPipeline> logger) : IToolExecutionPipeline
{
    private readonly ToolExecutionOptions _options = options.Value;

    public Task<object?> ExecuteAsync(ToolRouteDecision decision, CancellationToken cancellationToken = default)
    {
        if (!decision.IsMandatory || decision.ToolName is null)
            throw new ArgumentException("A mandatory tool route is required.", nameof(decision));
        return ExecuteWithPolicyAsync(decision.ToolName, decision.Arguments, ToolInvocationMode.Deterministic,
            decision.StepOrder,
            _ => Task.FromResult(Invoke(decision.ToolName, decision.Arguments)), cancellationToken);
    }

    public T ExecuteModelSelected<T>(string toolName, object input, Func<T> operation) =>
        (T)ExecuteWithPolicyAsync(toolName, input, ToolInvocationMode.ModelSelected, null,
            _ => Task.FromResult<object?>(operation()), CancellationToken.None).GetAwaiter().GetResult()!;

    internal async Task<object?> ExecuteWithPolicyAsync(string toolName, object input, ToolInvocationMode mode,
        int? planStepOrder,
        Func<CancellationToken, Task<object?>> operation, CancellationToken cancellationToken)
    {
        logger.LogInformation("Tool selected: {Tool}; invocation mode: {Mode}", toolName, mode);
        int retries = 0;
        DateTimeOffset startedAt = timeProvider.GetUtcNow();
        while (true)
        {
            try
            {
                using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
                object? output = await operation(timeout.Token).WaitAsync(timeout.Token);
                traceRecorder.RecordToolExecution(toolName, mode, planStepOrder, startedAt, timeProvider.GetUtcNow(), retries,
                    "Success", false, input, output, null);
                logger.LogInformation("Tool {Tool} completed after {RetryCount} retries", toolName, retries);
                return output;
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                traceRecorder.RecordToolExecution(toolName, mode, planStepOrder, startedAt, timeProvider.GetUtcNow(), retries,
                    "Timeout", true, input, null, $"Timed out after {_options.TimeoutSeconds} seconds.");
                logger.LogError(exception, "Tool {Tool} timed out after {TimeoutSeconds} seconds", toolName, _options.TimeoutSeconds);
                throw new ToolExecutionFailedException(toolName, "the execution timed out", exception);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TransientToolException exception) when (retries < _options.MaximumRetries)
            {
                retries++;
                logger.LogWarning(exception, "Transient failure for {Tool}; retry {RetryCount} of {MaximumRetries}",
                    toolName, retries, _options.MaximumRetries);
            }
            catch (Exception exception)
            {
                traceRecorder.RecordToolExecution(toolName, mode, planStepOrder, startedAt, timeProvider.GetUtcNow(), retries,
                    "Failure", false, input, null, exception.Message);
                logger.LogError(exception, "Tool {Tool} failed after {RetryCount} retries", toolName, retries);
                throw exception is ToolExecutionFailedException ? exception :
                    new ToolExecutionFailedException(toolName, exception.Message, exception);
            }
        }
    }

    private object Invoke(string toolName, IReadOnlyDictionary<string, object?> args) => toolName switch
    {
        "DistanceTool" => distance.GetDistance((string)args["origin"]!, (string)args["destination"]!),
        "CurrencyTool" => currency.ConvertCurrency((string)args["from"]!, (string)args["to"]!, (decimal)args["amount"]!),
        "TimeZoneTool" => timeZone.GetLocalTime((string)args["city"]!),
        "WeatherTool" => weather.GetWeather((string)args["destination"]!),
        _ => throw new InvalidOperationException($"Unknown mandatory tool '{toolName}'.")
    };
}
