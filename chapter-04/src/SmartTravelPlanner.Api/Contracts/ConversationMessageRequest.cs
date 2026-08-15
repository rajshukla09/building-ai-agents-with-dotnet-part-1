using System.ComponentModel.DataAnnotations;

namespace SmartTravelPlanner.Api.Contracts;

public sealed record ConversationMessageRequest(
    [Required, StringLength(2_000)] string Message);
