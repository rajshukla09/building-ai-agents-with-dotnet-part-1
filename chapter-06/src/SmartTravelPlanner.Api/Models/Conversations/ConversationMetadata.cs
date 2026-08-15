namespace SmartTravelPlanner.Api.Models.Conversations;

public sealed record ConversationMetadata(
    Guid ConversationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt,
    DateTimeOffset ExpirationTime,
    int MessageCount,
    SessionStatus Status);
