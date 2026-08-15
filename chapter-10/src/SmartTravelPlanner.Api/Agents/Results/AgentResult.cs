namespace SmartTravelPlanner.Api.Agents.Results;

public sealed record AgentResult<T>
{
    private AgentResult(bool isSuccess, T? value, AgentFailure? failure, AgentExecutionMetadata metadata)
    {
        if (isSuccess && value is null)
            throw new ArgumentException("Successful agent results must include a value.", nameof(value));
        if (!isSuccess && failure is null)
            throw new ArgumentException("Failed agent results must include a failure.", nameof(failure));

        IsSuccess = isSuccess;
        Value = value;
        Failure = failure;
        Metadata = metadata;
    }

    public bool IsSuccess { get; }
    public T? Value { get; }
    public AgentFailure? Failure { get; }
    public AgentExecutionMetadata Metadata { get; }

    public static AgentResult<T> Success(T value, AgentExecutionMetadata metadata) => new(true, value, null, metadata);
    public static AgentResult<T> Failed(AgentFailure failure, AgentExecutionMetadata metadata) => new(false, default, failure, metadata);
}
