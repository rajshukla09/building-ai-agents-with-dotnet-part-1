# Chapter 6: Adding AI Tools to the Travel Agent

Chapter 6 extends the persistent Smart Travel Planner from Chapter 5 with four local,
deterministic tools. The language model remains responsible for understanding a request and
writing a structured `TripPlan`, but it can now obtain application-owned facts instead of relying
only on knowledge learned during training.

## Why tools?

An LLM is useful for language and planning, but its internal knowledge can be stale, approximate,
or unsupported. Tool execution gives the model an explicit operation with typed inputs and an
application-controlled result. In this sample the distinction is easy to observe: the model
decides that it needs weather, while `WeatherTool` supplies the value the itinerary must use.

## Local deterministic tools

| Tool | Function | Sample data |
| --- | --- | --- |
| `WeatherTool` | `GetWeather(destination)` | temperature, condition, recommendation |
| `CurrencyTool` | `ConvertCurrency(from, to, amount)` | fixed USD-based rates for USD, INR, EUR, SAR |
| `TimeZoneTool` | `GetLocalTime(city)` | injected clock plus fixed city UTC offsets |
| `DistanceTool` | `GetDistance(origin, destination)` | fixed distances in kilometres |

Every tool is an independent class under `Tools/`. None calls an external service. Unknown
weather destinations receive an explicit generic sample; unsupported currencies, time zones, and
routes are rejected rather than invented.

## Registration and automatic selection

`Program.cs` registers each tool with ASP.NET Core dependency injection. `TravelAgent` receives
the tool instances through its constructor and exposes their methods with Agent Framework's
`AIFunctionFactory`. Function and parameter descriptions provide selection context to the model;
the system instructions state when each operation is mandatory and allow multiple operations in
one turn. No service locator or manual request router is used.

Tools also work during persisted conversations because they are attached to the same `AIAgent`
that creates, serializes, restores, and runs every `AgentSession`. Tool results enrich the summary,
activities, or notes. `TripPlan` remains unchanged and is wrapped by `TripPlanResponse` alongside
an execution trace.

## Execution tracing

Every standalone plan and conversation turn creates an isolated trace. The response records the
request's start, completion, total duration, and an ordered entry for each tool call. Entries
include safe typed inputs and outputs, timestamps, duration, and either `Success` or `Failure` with
an error. A request that needs no tool returns an empty `toolCalls` array.

Tracing is deliberately separate from the business-facing `TripPlan`: itinerary consumers do not
need observability fields in their domain model, while operators and learning clients can inspect
how the answer was grounded. The recorder uses `TimeProvider` and `AsyncLocal` request context, so
parallel agent requests do not share calls and timing tests do not depend on wall-clock time. This
boundary also prepares later observability, MCP, and multi-agent chapters to evolve execution
metadata without changing travel-planning data.

Example response (timestamps and durations vary):

```json
{
  "tripPlan": { "destination": "Jaipur", "durationDays": 2, "summary": "...", "days": [] },
  "execution": {
    "startedAt": "2026-08-03T08:30:00Z",
    "completedAt": "2026-08-03T08:30:00.250Z",
    "totalDurationMs": 250,
    "toolCalls": [
      {
        "order": 1,
        "toolName": "WeatherTool",
        "startedAt": "2026-08-03T08:30:00.100Z",
        "completedAt": "2026-08-03T08:30:00.108Z",
        "durationMs": 8,
        "status": "Success",
        "input": { "destination": "Jaipur" },
        "output": {
          "temperature": 31,
          "condition": "Sunny",
          "recommendation": "Carry sunscreen and water."
        },
        "error": null
      }
    ]
  }
}
```

## Run

```bash
dotnet restore SmartTravelPlanner.sln
dotnet build SmartTravelPlanner.sln --no-restore
dotnet test SmartTravelPlanner.sln --no-build
dotnet run --project src/SmartTravelPlanner.Api
```

Try `Plan a trip to Jaipur`, `How much is 100 USD in INR?`, `What time is it in
Tokyo?`, and `How far is Hyderabad from Jaipur?` in a conversation.

## Current limitations

- All values are educational samples, not live observations or quotes.
- The supported currency, time-zone, and distance tables are deliberately small.
- UTC offsets do not model daylight-saving transitions.
- Tool selection is ultimately performed by the configured model and therefore requires a valid
  Azure OpenAI configuration for end-to-end manual verification.
- Trace error text is intentionally limited to the controlled local tool failures in this chapter;
  future production tools should apply application-specific redaction before recording errors.
