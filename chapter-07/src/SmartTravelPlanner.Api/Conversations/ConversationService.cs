using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;
using SmartTravelPlanner.Api.Agents;
using SmartTravelPlanner.Api.Configuration;
using SmartTravelPlanner.Api.Models.Conversations;
using SmartTravelPlanner.Api.Models.TravelPlanning;
using SmartTravelPlanner.Api.Models.Execution;
using SmartTravelPlanner.Api.Memory;
using SmartTravelPlanner.Api.Travelers;

namespace SmartTravelPlanner.Api.Conversations;

public sealed class ConversationService(
    ITravelAgent travelAgent,
    IConversationStore conversationStore,
    TimeProvider timeProvider,
    IOptions<SessionLifecycleOptions> options,
    TravelerMemoryService? memoryService = null,
    ITravelerStore? travelerStore = null) : IConversationService
{
    private SessionLifecycleOptions LifecycleOptions => options.Value;

    public Task<ConversationMetadata> CreateAsync(CancellationToken cancellationToken = default) =>
        CreateAsync(Guid.NewGuid(), cancellationToken);

    public async Task<ConversationMetadata> CreateAsync(Guid travelerId, CancellationToken cancellationToken = default)
    {
        if (travelerId == Guid.Empty) throw new ArgumentException("TravelerId cannot be empty.", nameof(travelerId));
        if (travelerStore is not null && !travelerStore.Exists(travelerId))
            throw new KeyNotFoundException("The traveller does not exist.");
        AgentSession session = await travelAgent.CreateSessionAsync(cancellationToken);
        ConversationState conversation = conversationStore.Add(session, travelerId);
        return conversation.ToMetadata(timeProvider.GetUtcNow(), LifecycleOptions);
    }

    public ConversationMetadata? Get(Guid conversationId)
    {
        if (!conversationStore.TryGet(conversationId, out ConversationState conversation))
        {
            return null;
        }

        ConversationMetadata metadata = conversation.ToMetadata(timeProvider.GetUtcNow(), LifecycleOptions);
        conversationStore.Update(conversation);
        return metadata;
    }

    public IReadOnlyCollection<ConversationMetadata> ListActive()
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        return conversationStore.GetAll()
            .Select(conversation =>
            {
                ConversationMetadata metadata = conversation.ToMetadata(now, LifecycleOptions);
                conversationStore.Update(conversation);
                return metadata;
            })
            .Where(metadata => metadata.Status is not (SessionStatus.Expired or SessionStatus.Removed))
            .OrderBy(metadata => metadata.CreatedAt)
            .ToArray();
    }

    public async Task<SendMessageResult> SendMessageAsync(
        Guid conversationId,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (!conversationStore.TryGet(conversationId, out ConversationState conversation))
            return new SendMessageResult(SendMessageOutcome.NotFound);
        return await SendMessageAsync(conversationId, conversation.TravelerId, message, cancellationToken);
    }

    public async Task<SendMessageResult> SendMessageAsync(
        Guid conversationId,
        Guid travelerId,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (!conversationStore.TryGet(conversationId, out ConversationState conversation))
        {
            return new SendMessageResult(SendMessageOutcome.NotFound);
        }

        // The conversation-to-traveller binding is authoritative and prevents cross-traveller disclosure.
        if (conversation.TravelerId != travelerId)
            return new SendMessageResult(SendMessageOutcome.NotFound);

        await conversation.TurnLock.WaitAsync(cancellationToken);
        try
        {
            SessionStatus status = conversation.RefreshStatus(timeProvider.GetUtcNow(), LifecycleOptions);
            if (status == SessionStatus.Removed)
            {
                return new SendMessageResult(SendMessageOutcome.NotFound);
            }

            if (status == SessionStatus.Expired)
            {
                conversationStore.Update(conversation);
                return new SendMessageResult(SendMessageOutcome.Expired);
            }

            string memoryContext = memoryService?.BuildContext(travelerId) ?? string.Empty;
            string enrichedMessage = string.IsNullOrEmpty(memoryContext) ? message : $"{memoryContext}\n\nCurrent request:\n{message}";
            TripPlanResponse response = await travelAgent.SendMessageAsync(enrichedMessage, conversation.Session, cancellationToken);
            conversation.RecordMessage(timeProvider.GetUtcNow(), LifecycleOptions);
            conversationStore.Update(conversation);
            return new SendMessageResult(SendMessageOutcome.Success, response);
        }
        finally
        {
            conversation.TurnLock.Release();
        }
    }

    public bool Expire(Guid conversationId)
    {
        if (!conversationStore.TryGet(conversationId, out ConversationState conversation))
        {
            return false;
        }

        conversation.TurnLock.Wait();
        try
        {
            conversation.Expire(timeProvider.GetUtcNow());
            conversationStore.Update(conversation);
            return true;
        }
        finally
        {
            conversation.TurnLock.Release();
        }
    }

    public bool Delete(Guid conversationId)
    {
        if (!conversationStore.TryGet(conversationId, out ConversationState conversation))
        {
            return false;
        }

        conversation.TurnLock.Wait();
        try
        {
            conversation.MarkRemoved();
            return conversationStore.Delete(conversationId);
        }
        finally
        {
            conversation.TurnLock.Release();
        }
    }

    public int CleanupExpired()
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        int removed = 0;
        foreach (ConversationState conversation in conversationStore.GetAll())
        {
            conversation.TurnLock.Wait();
            try
            {
                if (conversation.RefreshStatus(now, LifecycleOptions) != SessionStatus.Expired)
                {
                    continue;
                }

                conversation.MarkRemoved();
                if (conversationStore.Delete(conversation.Id))
                {
                    removed++;
                }
            }
            finally
            {
                conversation.TurnLock.Release();
            }
        }

        return removed;
    }
}
