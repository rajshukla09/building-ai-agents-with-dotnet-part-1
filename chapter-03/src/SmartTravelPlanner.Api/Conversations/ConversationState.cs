using Microsoft.Agents.AI;
using SmartTravelPlanner.Api.Models.Conversations;

namespace SmartTravelPlanner.Api.Conversations;

public sealed class ConversationState
{
    private int _messageCount;

    public ConversationState(Guid id, AgentSession session, DateTimeOffset createdAt)
    {
        Id = id;
        Session = session;
        CreatedAt = createdAt;
        LastActivityAt = createdAt;
    }

    public Guid Id { get; }

    public AgentSession Session { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset LastActivityAt { get; private set; }

    public SemaphoreSlim TurnLock { get; } = new(1, 1);

    public void RecordMessage(DateTimeOffset timestamp)
    {
        _messageCount++;
        LastActivityAt = timestamp;
    }

    public void ReplaceSession(AgentSession session) => Session = session;

    public ConversationMetadata ToMetadata() =>
        new(Id, CreatedAt, LastActivityAt, _messageCount);
}
