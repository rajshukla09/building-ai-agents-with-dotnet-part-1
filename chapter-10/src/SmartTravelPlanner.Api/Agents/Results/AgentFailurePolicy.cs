namespace SmartTravelPlanner.Api.Agents.Results;

public interface IAgentFailurePolicy
{
    AgentFailureAction Decide(AgentFailure failure);
}

public enum AgentFailureAction
{
    Stop,
    Retry,
    Regenerate,
    Fallback,
    RequireHumanReview
}

public sealed class DefaultAgentFailurePolicy : IAgentFailurePolicy
{
    public AgentFailureAction Decide(AgentFailure failure) => failure.Kind switch
    {
        AgentFailureKind.StructuredOutput => AgentFailureAction.Regenerate,
        AgentFailureKind.Validation => AgentFailureAction.Regenerate,
        AgentFailureKind.RateLimit => AgentFailureAction.Retry,
        AgentFailureKind.Timeout => AgentFailureAction.Retry,
        AgentFailureKind.Infrastructure => AgentFailureAction.Stop,
        _ => AgentFailureAction.Stop
    };
}
