# Chapter 4 – Managing Agent Sessions

## Objective

Chapter 4 copies the complete Chapter 3 application and adds explicit lifecycle management for multiple independent Microsoft Agent Framework `AgentSession` instances. Chapter 3 conversation continuity, the structured `TripPlan`, Azure OpenAI configuration, Swagger, request validation, and the standalone travel endpoint remain available.

An agent session is the framework object that carries one conversation's turn context. Keeping one session per conversation allows a follow-up to understand prior turns, while separate session objects prevent one user or trip from seeing another conversation. Lifecycle management matters because abandoned in-memory sessions otherwise remain usable and consume process resources indefinitely.

## Lifecycle states

| State | Meaning |
| --- | --- |
| `Created` | The session exists but has not completed a message. |
| `Active` | A message completed successfully within the idle window. |
| `Idle` | The session has been inactive for the configured idle timeout but can still resume. |
| `Expired` | The expiration deadline passed or an operator explicitly expired the session; messages are rejected. |
| `Removed` | The session was deleted from the in-memory store. |

Successful activity resets `lastActivityAt`, moves the session to `Active`, and sets `expirationTime` to the new activity time plus the expiration timeout. Status evaluation moves an active session to `Idle` after `IdleTimeoutMinutes` and to `Expired` at `expirationTime`. The defaults are 15 and 30 minutes respectively; expiration must be later than idle transition.

Each conversation retains its own `AgentSession` and `SemaphoreSlim`. Turns, explicit expiration, deletion, and cleanup coordinate through that per-session lock. Different sessions can proceed concurrently.

## Configuration

`appsettings.json` contains:

```json
"SessionLifecycle": {
  "IdleTimeoutMinutes": 15,
  "ExpirationTimeoutMinutes": 30
}
```

Override these with configuration or `SessionLifecycle__IdleTimeoutMinutes` and `SessionLifecycle__ExpirationTimeoutMinutes`. Values are validated at startup.

## Build and run

From `chapter-04`, configure the same Azure OpenAI endpoint, API key, and deployment used in earlier chapters, then run:

```bash
dotnet restore
dotnet build
dotnet test
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/SmartTravelPlanner.Api
```

## APIs

Chapter 3 conversation routes remain unchanged:

```text
POST   /api/conversations
GET    /api/conversations/{id}
POST   /api/conversations/{id}/messages
DELETE /api/conversations/{id}
```

Sending to an expired session returns `410 Gone`; an unknown or removed ID returns `404 Not Found`. Session-management routes are:

```text
GET    /api/sessions                  List non-expired sessions
GET    /api/sessions/{id}             Retrieve lifecycle metadata
POST   /api/sessions/{id}/expire      Explicitly expire a session
DELETE /api/sessions/{id}             Remove a session
POST   /api/sessions/cleanup          Remove all currently expired sessions
```

Metadata includes `conversationId`, `createdAt`, `lastActivityAt`, `expirationTime`, `messageCount`, and `status`. In Development, use `/swagger` to create multiple conversations, list their sessions, send independent messages, expire one, verify its `410` response, and run cleanup.

## Cleanup and limitations

Cleanup reevaluates every stored session against the injected `TimeProvider` and removes those in `Expired`. Tests use a manual clock, so idle and expiration transitions require no real waiting.

Storage is still process-local. Sessions disappear on restart, are not shared between application instances, and cleanup runs only when its endpoint is invoked. Chapter 4 deliberately adds no SQL Server, Redis, Cosmos DB, durable persistence, memory or context provider, tool, workflow, or multi-agent logic.
