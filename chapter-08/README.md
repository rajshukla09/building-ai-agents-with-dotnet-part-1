# Chapter 8 — Adding Context Providers

Chapter 8 replaces Chapter 7's prompt concatenation with Microsoft Agent Framework `AIContextProvider` implementations. The unchanged user message flows through `TravelerMemoryContextProvider` and `RuntimeTravelContextProvider` before `TravelAgent` invokes the model.

## History, memory, and runtime context

These three kinds of state have different lifetimes:

* **Conversation history** belongs to `AgentSession`, supports follow-up turns, and is persisted with the conversation.
* **Long-term memory** belongs to one `travelerId` in `TravelerMemoryStore` and contains explicit reusable food, interest, pace, budget, and accessibility preferences.
* **Runtime context** describes one invocation: UTC time, traveler and conversation identifiers, session status, and destination/duration when available.

Providers perform pre-invocation enrichment through `AIContext.Instructions`. That invocation-only context helps the model but is not appended to the user message and therefore is not automatically stored as chat history. A new invocation retrieves it again.

## Providers and filtering

`TravelerMemoryContextProvider` resolves the traveler from the server-controlled conversation binding, reads only that traveler's record, filters out unspecified fields, and supplies no context when memory is absent. `RuntimeTravelContextProvider` always supplies the current UTC timestamp and adds only metadata available for the request. The providers are registered, in deterministic memory-then-runtime order, in `ChatClientAgentOptions.AIContextProviders`.

Provider failures are logged and degrade to empty context rather than exposing memory or failing a trip. Execution traces contain provider name, order, duration, category, whether context was added, and success/failure—but never context values.

## API flow

1. `POST /api/travelers` and save explicit memory with `PUT /api/travelers/{travelerId}/memory`.
2. `POST /api/conversations?travelerId={travelerId}`.
3. `POST /api/conversations/{conversationId}/messages` with only the actual request.
4. Inspect `executionTrace.contextProviders` for both providers.

Conversation-to-traveler binding is authoritative, preventing cross-traveler disclosure. Production deployments should additionally use authentication, authorization, encrypted storage, retention policies, and a transactional database.

## Run

```bash
dotnet restore
dotnet build
dotnet test
```
