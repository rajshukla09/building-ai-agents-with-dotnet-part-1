using System.Collections.Concurrent;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;
using SmartTravelPlanner.Api.Configuration;

namespace SmartTravelPlanner.Api.Conversations;

public sealed class InMemoryConversationStore(
    TimeProvider timeProvider,
    IOptions<SessionLifecycleOptions> options) : IConversationStore
{
    private readonly ConcurrentDictionary<Guid, ConversationState> _conversations = new();

    public ConversationState Add(AgentSession session)
    {
        ConversationState conversation;
        do
        {
            Guid id = Guid.NewGuid();
            conversation = new ConversationState(
                id,
                session,
                timeProvider.GetUtcNow(),
                options.Value.ExpirationTimeout);
        }
        while (!_conversations.TryAdd(conversation.Id, conversation));

        return conversation;
    }

    public bool TryGet(Guid conversationId, out ConversationState conversation) =>
        _conversations.TryGetValue(conversationId, out conversation!);

    public IReadOnlyCollection<ConversationState> GetAll() => _conversations.Values.ToArray();

    public void Update(ConversationState conversation)
    {
        // State is held by reference, so the in-memory implementation has nothing to flush.
    }

    public bool Delete(Guid conversationId) =>
        _conversations.TryRemove(conversationId, out _);
}
