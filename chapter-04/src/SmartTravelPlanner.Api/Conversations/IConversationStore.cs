namespace SmartTravelPlanner.Api.Conversations;

public interface IConversationStore
{
    ConversationState Add(Microsoft.Agents.AI.AgentSession session);

    bool TryGet(Guid conversationId, out ConversationState conversation);

    IReadOnlyCollection<ConversationState> GetAll();

    bool Delete(Guid conversationId);
}
