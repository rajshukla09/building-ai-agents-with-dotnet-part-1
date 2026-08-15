using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
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
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);
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

        TripPlan? plan = await conversationService.SendMessageAsync(
            conversationId,
            request.Message,
            cancellationToken);
        return plan is null ? NotFound() : Ok(plan);
    }

    [HttpPost("{conversationId:guid}/messages/stream")]
    [Produces("application/x-ndjson")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task StreamMessageAsync(
        Guid conversationId,
        ConversationMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    [nameof(request.Message)] = ["Message is required and cannot contain only whitespace."]
                }), cancellationToken);
            return;
        }

        IAsyncEnumerable<string>? updates = conversationService.StreamMessageAsync(
            conversationId,
            request.Message,
            cancellationToken);
        if (updates is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "application/x-ndjson";
        Response.Headers.CacheControl = "no-cache";
        await WriteUpdateAsync(new ConversationStreamUpdate("generating"), CancellationToken.None);

        try
        {
            await foreach (string delta in updates.WithCancellation(cancellationToken))
            {
                await WriteUpdateAsync(new ConversationStreamUpdate("generating", delta), cancellationToken);
            }

            await WriteUpdateAsync(new ConversationStreamUpdate("completed"), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                await WriteUpdateAsync(new ConversationStreamUpdate("cancelled"), CancellationToken.None);
            }
        }
        catch (Exception)
        {
            await WriteUpdateAsync(
                new ConversationStreamUpdate("failed", Error: "The response could not be completed."),
                CancellationToken.None);
        }
    }

    private async Task WriteUpdateAsync(ConversationStreamUpdate update, CancellationToken cancellationToken)
    {
        await JsonSerializer.SerializeAsync(
            Response.Body,
            update,
            StreamJsonOptions,
            cancellationToken);
        await Response.WriteAsync("\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    [HttpDelete("{conversationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(Guid conversationId) =>
        conversationService.Delete(conversationId) ? NoContent() : NotFound();
}
