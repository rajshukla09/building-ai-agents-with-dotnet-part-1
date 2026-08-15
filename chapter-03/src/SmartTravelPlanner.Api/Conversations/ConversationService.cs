using Microsoft.Agents.AI;
using SmartTravelPlanner.Api.Agents;
using SmartTravelPlanner.Api.Models.Conversations;
using SmartTravelPlanner.Api.Models.TravelPlanning;

namespace SmartTravelPlanner.Api.Conversations;

public sealed class ConversationService(
    ITravelAgent travelAgent,
    IConversationStore conversationStore,
    TimeProvider timeProvider) : IConversationService
{
    public async Task<ConversationMetadata> CreateAsync(CancellationToken cancellationToken = default)
    {
        AgentSession session = await travelAgent.CreateSessionAsync(cancellationToken);
        return conversationStore.Add(session).ToMetadata();
    }

    public ConversationMetadata? Get(Guid conversationId) =>
        conversationStore.TryGet(conversationId, out ConversationState conversation)
            ? conversation.ToMetadata()
            : null;

    public async Task<TripPlan?> SendMessageAsync(
        Guid conversationId,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (!conversationStore.TryGet(conversationId, out ConversationState conversation))
        {
            return null;
        }

        await conversation.TurnLock.WaitAsync(cancellationToken);
        try
        {
            TripPlan plan = await travelAgent.SendMessageAsync(message, conversation.Session, cancellationToken);
            conversation.RecordMessage(timeProvider.GetUtcNow());
            return plan;
        }
        finally
        {
            conversation.TurnLock.Release();
        }
    }

    public bool Delete(Guid conversationId) => conversationStore.Delete(conversationId);

    public IAsyncEnumerable<string>? StreamMessageAsync(
        Guid conversationId,
        string message,
        CancellationToken cancellationToken = default) =>
        conversationStore.TryGet(conversationId, out ConversationState conversation)
            ? StreamTurnAsync(conversation, message, cancellationToken)
            : null;

    private async IAsyncEnumerable<string> StreamTurnAsync(
        ConversationState conversation,
        string message,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await conversation.TurnLock.WaitAsync(cancellationToken);
        try
        {
            AgentSession workingSession = await travelAgent.CloneSessionAsync(
                conversation.Session,
                cancellationToken);

            await foreach (string update in travelAgent
                .StreamMessageAsync(message, workingSession, cancellationToken)
                .WithCancellation(cancellationToken))
            {
                yield return update;
            }

            cancellationToken.ThrowIfCancellationRequested();
            conversation.ReplaceSession(workingSession);
            conversation.RecordMessage(timeProvider.GetUtcNow());
        }
        finally
        {
            conversation.TurnLock.Release();
        }
    }
}
