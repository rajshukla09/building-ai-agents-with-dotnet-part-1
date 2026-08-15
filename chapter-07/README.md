# Chapter 7 — Building Memory-Aware Agents

Chapter 7 extends the Chapter 6 Smart Travel Planner with durable, traveller-scoped preferences while preserving its sessions, JSON conversation persistence, tools, structured responses, and execution tracing.

## Conversation history and long-term memory

Conversation history belongs to one agent session and supports follow-up turns such as “make day two quieter.” Long-term memory contains only explicit, reusable preferences: food, activity interests, pace, budget, and accessibility requirements. Entire messages and conversations are never copied into memory, and the application does not infer sensitive traits.

A `conversationId` identifies a short-lived planning session. A separate `travelerId` identifies the person whose preferences may be reused by several new conversations. First create the durable identity with `POST /api/travelers`, then create a bound conversation with `POST /api/conversations?travelerId={travelerId}`. Unknown traveller IDs return 404. Message requests contain only `message`; the server resolves the traveller from the conversation's stored binding, so clients cannot accidentally or deliberately switch traveller identity in a later turn.

## Traveller API

```http
POST   /api/travelers
GET    /api/travelers/{travelerId}
DELETE /api/travelers/{travelerId}
```

Creation returns a `TravelerProfile` containing a generated GUID. The profile is identity only: agent session state remains in the conversation store and reusable preferences remain in the memory store. Deleting a traveller also deletes that traveller's memory, but the three models and persistence documents remain separate.

## Memory API

```http
GET    /api/travelers/{travelerId}/memory
PUT    /api/travelers/{travelerId}/memory
DELETE /api/travelers/{travelerId}/memory
```

`PUT` adds or replaces the traveller's preference set. Before each conversation turn, `TravelerMemoryService` retrieves only the memory associated with the conversation's bound traveller and prepends a clearly labelled preference context to the current request. Deleting a conversation does not delete memory, and deleting memory does not delete conversation state.

The intended flow is: create traveller → save traveller memory → create a conversation for that traveller → retrieve memory → enrich the agent request.

Send a message to the bound conversation without repeating `travelerId`:

```http
POST /api/conversations/{conversationId}/messages
Content-Type: application/json

{
  "message": "Plan a relaxed three-day trip to Tokyo."
}
```

Example body:

```json
{
  "foodPreferences": ["vegetarian"],
  "activityInterests": ["gardens", "museums"],
  "travelPace": "relaxed",
  "budgetPreference": "mid-range",
  "accessibilityRequirements": ["step-free access"]
}
```

## Isolation and persistence

Conversation documents store the traveller binding, but memories live separately in `App_Data/traveler-memories.json`. The server uses that binding as the authority and returns 404 for a supplied traveller ID that does not match, preventing one traveller's preferences from entering another traveller's prompt.

JSON persistence is intentionally educational: writes are local and process-coordinated, there is no distributed locking, encryption, database transaction, query indexing, or multi-instance consistency. Use protected database storage, authorization, retention controls, and concurrency/version checks in production.

## Run

```bash
dotnet restore
dotnet build
dotnet test
```
