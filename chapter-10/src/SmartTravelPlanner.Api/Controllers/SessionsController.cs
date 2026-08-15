using Microsoft.AspNetCore.Mvc;
using SmartTravelPlanner.Api.Conversations;
using SmartTravelPlanner.Api.Models.Conversations;

namespace SmartTravelPlanner.Api.Controllers;

[ApiController]
[Route("api/sessions")]
[Produces("application/json")]
public sealed class SessionsController(IConversationService conversationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<ConversationMetadata>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyCollection<ConversationMetadata>> ListActive() =>
        Ok(conversationService.ListActive());

    [HttpGet("{sessionId:guid}")]
    [ProducesResponseType(typeof(ConversationMetadata), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ConversationMetadata> Get(Guid sessionId)
    {
        ConversationMetadata? session = conversationService.Get(sessionId);
        return session is null ? NotFound() : Ok(session);
    }

    [HttpPost("{sessionId:guid}/expire")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Expire(Guid sessionId) =>
        conversationService.Expire(sessionId) ? NoContent() : NotFound();

    [HttpDelete("{sessionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(Guid sessionId) =>
        conversationService.Delete(sessionId) ? NoContent() : NotFound();

    [HttpPost("cleanup")]
    [ProducesResponseType(typeof(SessionCleanupResult), StatusCodes.Status200OK)]
    public ActionResult<SessionCleanupResult> CleanupExpired() =>
        Ok(new SessionCleanupResult(conversationService.CleanupExpired()));
}
