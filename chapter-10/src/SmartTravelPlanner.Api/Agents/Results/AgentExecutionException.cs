namespace SmartTravelPlanner.Api.Agents.Results;

public sealed class AgentExecutionException : Exception
{
    public AgentExecutionException(AgentFailure failure, AgentExecutionMetadata metadata, Exception? innerException = null)
        : base(failure.Message, innerException)
    {
        Failure = failure;
        Metadata = metadata;
    }

    public AgentFailure Failure { get; }
    public AgentExecutionMetadata Metadata { get; }
}
