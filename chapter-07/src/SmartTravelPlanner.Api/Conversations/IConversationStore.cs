namespace SmartTravelPlanner.Api.Conversations;

public interface IConversationStore
{
    ConversationState Add(Microsoft.Agents.AI.AgentSession session, Guid travelerId);

    bool TryGet(Guid conversationId, out ConversationState conversation);

    IReadOnlyCollection<ConversationState> GetAll();

    void Update(ConversationState conversation);

    bool Delete(Guid conversationId);
}
