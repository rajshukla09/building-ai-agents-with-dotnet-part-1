using SmartTravelPlanner.Api.Models.Conversations;
using SmartTravelPlanner.Api.Models.TravelPlanning;

namespace SmartTravelPlanner.Api.Conversations;

public interface IConversationService
{
    Task<ConversationMetadata> CreateAsync(CancellationToken cancellationToken = default);

    ConversationMetadata? Get(Guid conversationId);

    Task<TripPlan?> SendMessageAsync(
        Guid conversationId,
        string message,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string>? StreamMessageAsync(
        Guid conversationId,
        string message,
        CancellationToken cancellationToken = default);

    bool Delete(Guid conversationId);
}
