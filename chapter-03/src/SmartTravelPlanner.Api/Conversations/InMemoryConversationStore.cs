using System.Collections.Concurrent;
using Microsoft.Agents.AI;

namespace SmartTravelPlanner.Api.Conversations;

public sealed class InMemoryConversationStore(TimeProvider timeProvider) : IConversationStore
{
    private readonly ConcurrentDictionary<Guid, ConversationState> _conversations = new();

    public ConversationState Add(AgentSession session)
    {
        ConversationState conversation;
        do
        {
            Guid id = Guid.NewGuid();
            conversation = new ConversationState(id, session, timeProvider.GetUtcNow());
        }
        while (!_conversations.TryAdd(conversation.Id, conversation));

        return conversation;
    }

    public bool TryGet(Guid conversationId, out ConversationState conversation) =>
        _conversations.TryGetValue(conversationId, out conversation!);

    public bool Delete(Guid conversationId) =>
        _conversations.TryRemove(conversationId, out _);
}
