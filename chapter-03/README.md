# Chapter 3 – Conversations That Continue

## Objective

Chapter 3 copies the complete Chapter 2 application and adds conversation continuity. The structured `TripPlan`, Azure OpenAI configuration, standalone travel-plan endpoint, validation, Swagger setup, dependency injection, safe error handling, and tests remain available.

An isolated request gives an agent only the current message. A follow-up such as “Make Day 2 less busy” is ambiguous unless the agent can see the earlier request and itinerary. Chapter 3 associates each conversation ID with a Microsoft Agent Framework `AgentSession`, allowing subsequent turns to use the same framework-managed conversation history.

## How continuity works

1. `POST /api/conversations` creates an `AgentSession` and stores it under a new GUID.
2. `POST /api/conversations/{id}/messages` retrieves that same session and passes it to `AIAgent.RunAsync<TripPlan>`.
3. The agent uses earlier turns to interpret follow-ups and returns the complete updated `TripPlan`.
4. Different IDs map to different sessions, so their histories remain isolated.
5. `GET` returns basic metadata; `DELETE` removes the in-memory entry. Unknown or deleted IDs return `404 Not Found`.

Turns within one conversation are serialized so simultaneous requests cannot mutate the same agent session at the same time. The store is process-local: all conversations disappear when the API restarts, are not shared between multiple application instances, and have no expiry policy. Persistence is intentionally deferred.

## From complete responses to streaming updates

The normal endpoint still calls `RunAsync<TripPlan>(...)` and waits for a complete structured response. The new streaming endpoint calls the real MAF `RunStreamingAsync(...)` API and forwards each `AgentResponseUpdate.Text` fragment immediately as newline-delimited JSON:

```text
User → API → MAF RunStreamingAsync → streaming updates → UI
```

`POST /api/conversations/{id}/messages/stream` produces records with `generating`, `completed`, `cancelled`, or `failed` status. It does not split an already completed response.

Open the application root (`/`) for the streaming conversation UI. The assistant bubble grows as updates arrive. **Stop** aborts the browser request, which cancels the HTTP request token and the running MAF operation.

To keep history valid, a streaming turn runs against a cloned `AgentSession`. The completed working session replaces the stored session only after the stream finishes successfully. Cancellation or failure discards the working session, so a subsequent message continues from the last completed turn.

## Configure, build, and run

Install the .NET 9 SDK and configure Azure OpenAI from `chapter-03`:

```bash
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://YOUR-RESOURCE.openai.azure.com/" --project src/SmartTravelPlanner.Api
dotnet user-secrets set "AzureOpenAI:ApiKey" "YOUR-API-KEY" --project src/SmartTravelPlanner.Api
dotnet user-secrets set "AzureOpenAI:DeploymentName" "YOUR-CHAT-DEPLOYMENT" --project src/SmartTravelPlanner.Api
dotnet restore
dotnet build
dotnet test
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/SmartTravelPlanner.Api
```

The equivalent environment variables are `AzureOpenAI__Endpoint`, `AzureOpenAI__ApiKey`, and `AzureOpenAI__DeploymentName`. Never commit credentials.

## Conversation API example

Create a conversation:

```bash
curl -i -X POST "https://localhost:<port>/api/conversations"
```

The `201 Created` response includes an ID and metadata:

```json
{
  "conversationId": "653ba298-6ded-4f25-93f9-25ec82a0162c",
  "createdAt": "2026-08-02T10:00:00+00:00",
  "lastActivityAt": "2026-08-02T10:00:00+00:00",
  "messageCount": 0
}
```

Use that ID for the initial request and follow-up:

```bash
curl -X POST "https://localhost:<port>/api/conversations/<conversation-id>/messages" \
  -H "Content-Type: application/json" \
  -d '{"message":"Plan a three-day trip to Jaipur."}'

curl -X POST "https://localhost:<port>/api/conversations/<conversation-id>/messages" \
  -H "Content-Type: application/json" \
  -d '{"message":"Make Day 2 less busy."}'
```

Each successful message returns the full structured `TripPlan`. Retrieve metadata or delete the conversation with:

```bash
curl "https://localhost:<port>/api/conversations/<conversation-id>"
curl -X DELETE "https://localhost:<port>/api/conversations/<conversation-id>"
```

Stream a conversational response with curl's response buffering disabled:

```bash
curl -N -X POST "https://localhost:<port>/api/conversations/<conversation-id>/messages/stream" \
  -H "Content-Type: application/json" \
  -d '{"message":"Make Day 2 less busy."}'
```

## Swagger

Run in Development, open `/swagger`, create a conversation, copy its `conversationId`, and use it in the message, streaming message, metadata, and delete operations. The original `POST /api/travel/plan` endpoint and non-streaming conversation endpoint remain unchanged.

## Deliberately excluded

Session persistence, databases, distributed storage, memory providers, context providers, tools, workflows, and multi-agent logic are reserved for later chapters.
