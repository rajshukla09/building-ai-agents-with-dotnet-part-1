using Microsoft.AspNetCore.Mvc;
using SmartTravelPlanner.Api.Contracts;
using SmartTravelPlanner.Api.Conversations;
using SmartTravelPlanner.Api.Models.Conversations;
using SmartTravelPlanner.Api.Models.TravelPlanning;

namespace SmartTravelPlanner.Api.Controllers;

[ApiController]
[Route("api/conversations")]
[Produces("application/json")]
public sealed class ConversationsController(IConversationService conversationService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ConversationMetadata), StatusCodes.Status201Created)]
    public async Task<ActionResult<ConversationMetadata>> CreateAsync(CancellationToken cancellationToken)
    {
        ConversationMetadata conversation = await conversationService.CreateAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { conversationId = conversation.ConversationId }, conversation);
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
    [ProducesResponseType(typeof(TripPlan), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    public async Task<ActionResult<TripPlan>> SendMessageAsync(
        Guid conversationId,
        ConversationMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            ModelState.AddModelError(nameof(request.Message), "Message is required and cannot contain only whitespace.");
            return ValidationProblem(ModelState);
        }

        SendMessageResult result = await conversationService.SendMessageAsync(
            conversationId,
            request.Message,
            cancellationToken);
        return result.Outcome switch
        {
            SendMessageOutcome.Success => Ok(result.Plan),
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
