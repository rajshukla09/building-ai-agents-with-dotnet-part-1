using System.Text.Json;
using SmartTravelPlanner.Api.Models.Conversations;

namespace SmartTravelPlanner.Api.Conversations;

public sealed record ConversationDocument(
    Guid Id,
    JsonElement AgentSession,
    SessionStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt,
    DateTimeOffset ExpirationTime,
    int MessageCount);

public sealed record ConversationStoreDocument(int Version, IReadOnlyCollection<ConversationDocument> Conversations);
