namespace SmartTravelPlanner.Api.Contracts;

public sealed record ConversationStreamUpdate(string Status, string? Delta = null, string? Error = null);
