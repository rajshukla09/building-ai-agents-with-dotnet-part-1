using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;
using SmartTravelPlanner.Api.Agents;
using SmartTravelPlanner.Api.Configuration;
using SmartTravelPlanner.Api.Models.Conversations;
using SmartTravelPlanner.Api.Models.TravelPlanning;

namespace SmartTravelPlanner.Api.Conversations;

public sealed class ConversationService(
    ITravelAgent travelAgent,
    IConversationStore conversationStore,
    TimeProvider timeProvider,
    IOptions<SessionLifecycleOptions> options) : IConversationService
{
    private SessionLifecycleOptions LifecycleOptions => options.Value;

    public async Task<ConversationMetadata> CreateAsync(CancellationToken cancellationToken = default)
    {
        AgentSession session = await travelAgent.CreateSessionAsync(cancellationToken);
        ConversationState conversation = conversationStore.Add(session);
        return conversation.ToMetadata(timeProvider.GetUtcNow(), LifecycleOptions);
    }

    public ConversationMetadata? Get(Guid conversationId) =>
        conversationStore.TryGet(conversationId, out ConversationState conversation)
            ? conversation.ToMetadata(timeProvider.GetUtcNow(), LifecycleOptions)
            : null;

    public IReadOnlyCollection<ConversationMetadata> ListActive()
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        return conversationStore.GetAll()
            .Select(conversation => conversation.ToMetadata(now, LifecycleOptions))
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
        {
            return new SendMessageResult(SendMessageOutcome.NotFound);
        }

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
                return new SendMessageResult(SendMessageOutcome.Expired);
            }

            TripPlan plan = await travelAgent.SendMessageAsync(message, conversation.Session, cancellationToken);
            conversation.RecordMessage(timeProvider.GetUtcNow(), LifecycleOptions);
            return new SendMessageResult(SendMessageOutcome.Success, plan);
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
