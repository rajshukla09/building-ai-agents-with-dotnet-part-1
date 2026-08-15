using SmartTravelPlanner.Api.Models.Conversations;

namespace SmartTravelPlanner.Api.Conversations;

public interface IConversationService
{
    Task<ConversationMetadata> CreateAsync(CancellationToken cancellationToken = default);

    Task<ConversationMetadata> CreateAsync(Guid travelerId, CancellationToken cancellationToken = default) =>
        CreateAsync(cancellationToken);

    ConversationMetadata? Get(Guid conversationId);

    IReadOnlyCollection<ConversationMetadata> ListActive();

    Task<SendMessageResult> SendMessageAsync(
        Guid conversationId,
        string message,
        CancellationToken cancellationToken = default);

    Task<SendMessageResult> SendMessageAsync(
        Guid conversationId,
        Guid travelerId,
        string message,
        CancellationToken cancellationToken = default) =>
        SendMessageAsync(conversationId, message, cancellationToken);

    bool Expire(Guid conversationId);

    bool Delete(Guid conversationId);

    int CleanupExpired();
}
