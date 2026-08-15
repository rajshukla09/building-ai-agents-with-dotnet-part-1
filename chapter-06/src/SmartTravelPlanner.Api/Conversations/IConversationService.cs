using SmartTravelPlanner.Api.Models.Conversations;

namespace SmartTravelPlanner.Api.Conversations;

public interface IConversationService
{
    Task<ConversationMetadata> CreateAsync(CancellationToken cancellationToken = default);

    ConversationMetadata? Get(Guid conversationId);

    IReadOnlyCollection<ConversationMetadata> ListActive();

    Task<SendMessageResult> SendMessageAsync(
        Guid conversationId,
        string message,
        CancellationToken cancellationToken = default);

    bool Expire(Guid conversationId);

    bool Delete(Guid conversationId);

    int CleanupExpired();
}
