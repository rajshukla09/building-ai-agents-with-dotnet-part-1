using Microsoft.AspNetCore.Mvc;
using SmartTravelPlanner.Api.Contracts;
using SmartTravelPlanner.Api.Conversations;
using SmartTravelPlanner.Api.Models.Conversations;
using SmartTravelPlanner.Api.Models.Execution;
using SmartTravelPlanner.Api.Travelers;

namespace SmartTravelPlanner.Api.Controllers;

[ApiController]
[Route("api/conversations")]
[Produces("application/json")]
public sealed class ConversationsController(IConversationService conversationService, ITravelerStore travelerStore) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ConversationMetadata), StatusCodes.Status201Created)]
    public async Task<ActionResult<ConversationMetadata>> CreateAsync(
        [FromQuery] Guid travelerId,
        CancellationToken cancellationToken)
    {
        if (travelerId == Guid.Empty || !travelerStore.Exists(travelerId))
            return NotFound();
        ConversationMetadata conversation = await conversationService.CreateAsync(travelerId, cancellationToken);
        return CreatedAtAction(nameof(Get), new
        {
            conversationId = conversation.ConversationId
        }, conversation);
    }

    [HttpGet("{conversationId:guid}")]
    [ProducesResponseType(typeof(ConversationMetadata), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ConversationMetadata> Get(Guid conversationId)
    {
        ConversationMetadata? conversation = conversationService.Get(conversationId);
        return conversation is null ? NotFound() : Ok(conversation);
    }

    [HttpPost("{conversationId:guid}/messages")]
    [ProducesResponseType(typeof(TripPlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    public async Task<ActionResult<TripPlanResponse>> SendMessageAsync(
        Guid conversationId,
        ConversationMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            ModelState.AddModelError(nameof(request.Message), "Message is required and cannot contain only whitespace.");
            return ValidationProblem(ModelState);
        }

        ConversationMetadata? conversation = conversationService.Get(conversationId);
        // Traveller identity is resolved from the conversation binding. It is deliberately not
        // accepted again in the message body, so malformed or spoofed IDs cannot switch identity.
        if (conversation is null || !travelerStore.Exists(conversation.TravelerId))
            return NotFound();
        SendMessageResult result = await conversationService.SendMessageAsync(
            conversationId, conversation.TravelerId, request.Message, cancellationToken);
        return result.Outcome switch
        {
            SendMessageOutcome.Success => Ok(result.Response),
            SendMessageOutcome.Expired => StatusCode(
                StatusCodes.Status410Gone,
                new ProblemDetails { Title = "The agent session has expired", Status = StatusCodes.Status410Gone }),
            _ => NotFound()
        };
    }

    [HttpDelete("{conversationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(Guid conversationId) =>
        conversationService.Delete(conversationId) ? NoContent() : NotFound();
}
