# Chapter 5 – Persisting Conversations and State

## Objective

Chapter 5 copies the complete Chapter 4 application and makes its conversations survive application restarts. The API and lifecycle behavior remain the same, but `JsonConversationStore` transparently mirrors the in-memory lookup to `App_Data/conversations.json`.

An in-memory store is fast but disappears with its process. Persistent storage records completed changes on disk and reconstructs them at startup, allowing a client to retrieve and continue the same conversation after restarting the API.

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

From `chapter-05`, configure the same Azure OpenAI endpoint, API key, and deployment used in earlier chapters, then run:

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

## JSON persistence and startup restoration

The versioned JSON root contains a `conversations` array. Each item contains `id`, `agentSession`, `status`, `createdAt`, `lastActivityAt`, `expirationTime`, and `messageCount`. These strongly typed documents deliberately exclude `SemaphoreSlim`, `TimeProvider`, loggers, and services.

At startup the store detects the file, parses each item independently, asks the agent to reconstruct its supported `AgentSession`, and rebuilds each runtime `ConversationState` with fresh locks. Missing and empty files mean no saved conversations. Invalid files are logged and ignored; invalid individual entries and duplicate IDs are skipped so one record cannot block the others.

Create, a successfully completed message, explicit expiration, deletion, and expired-session cleanup automatically write a complete snapshot. The snapshot is first written to a temporary file and atomically moved over the destination so readers never observe a partial update. Consequently no shutdown hook or Save button is needed.

## Cleanup and limitations

Cleanup reevaluates every stored session against the injected `TimeProvider` and removes those in `Expired`. Tests use a manual clock, so idle and expiration transitions require no real waiting.

JSON is approachable, inspectable, and requires no database setup, making it appropriate for this chapter. Rewriting one local file is not ideal for large datasets or production: it offers no multi-process coordination, querying, access control, or horizontal scaling. The framework-owned session JSON is opaque and requires a compatible configured agent to restore it. Cleanup still runs only when its endpoint is invoked. SQL Server, Entity Framework, Redis, Cosmos DB, distributed caching, memory/context providers, authentication, and multi-agent logic are intentionally excluded.
