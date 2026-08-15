# Chapter 9 — Reliable Tool Execution

Chapter 9 copies the complete Chapter 8 application and replaces direct tool calls with one observable execution pipeline. The sample remains self-contained: Weather, Currency, Distance, and TimeZone return deterministic sample data and no external API, MCP, RAG, or multi-agent workflow is introduced.

## Deterministic and model-selected execution

`IRequestClassifier` sends every user turn to a dedicated Microsoft Agent Framework agent and requests a structured `ExecutionPlan`. The typed envelope contains the primary `RequestIntent` and zero, one, or many ordered `ExecutionStep` values. Each step names a `ToolType` and arguments. Every step is validated before routing by `ExecutionPlanValidator`: distance requires origin and destination; currency requires a non-negative amount and both currencies; local time requires a city; weather requires a destination. Argument names are matched case-insensitively. An invalid initial plan receives exactly one structured repair attempt, is revalidated, and produces a meaningful application error if it is still invalid. No tool executes until final validation succeeds.

`IToolRouter` receives one validated step at a time and switches only on `ToolType`; it never parses raw text. `ExecutionPlanExecutor` preserves step order, runs every step through `IToolExecutionPipeline`, and combines all successes and explicit failures into one enrichment block before the travel agent runs. A plain itinerary can have no deterministic steps and still works normally.

## Reliable execution pipeline

Every direct or model-selected invocation follows the same path:

```text
request -> RequestClassifier -> ExecutionPlan -> ordered steps -> ToolExecutionPipeline -> one enrichment block -> TravelAgent -> TripPlan
```

`ToolExecutionPipeline` logs selection and completion, applies the configured timeout, retries only `TransientToolException`, and translates terminal errors into a meaningful `ToolExecutionFailedException`. Invalid input and other permanent failures are never retried. Cancellation by the caller is kept distinct from a pipeline timeout.

Configuration defaults to three retries and a five-second timeout:

```json
"ToolExecution": {
  "MaximumRetries": 3,
  "TimeoutSeconds": 5
}
```

A failed deterministic plan step is traced and included explicitly in the combined enrichment block; independent later steps still run. The travel agent must qualify unavailable information rather than inventing a result. Model-selected failures likewise remain visible to the agent runtime and are never silently discarded.

## Observability and validation

Plan telemetry records classifier duration, the initial validation result and errors, whether repair was attempted, the repaired plan, the final validation result and errors, and total plan execution duration. Tool traces also carry the originating plan-step order. Each tool trace entry records order, tool, invocation mode (`Deterministic` or `ModelSelected`), start/end timestamps, duration, retry count, status, failure reason, timeout flag, input, and output. One final entry represents the complete logical invocation, including all retries. Ordering is assigned when an invocation completes and is preserved in the response.

Tests verify no-tool, single-tool, and four-tool plans, successful and failed one-shot repairs, case-insensitive and missing-argument validation, enum-only routing, partial failure, combined enrichment, successful and exhausted retry paths, timeout recording, execution ordering, invocation mode, and the full Chapter 8 conversation, persistence, memory, and context-provider behavior.

## Run

```bash
dotnet restore
dotnet build
dotnet test
```

## Why typed classification

Regex routing couples phrasing, parsing, validation, and dispatch, and becomes brittle as intents and languages grow. Structured output delegates language interpretation to the classifier while keeping the application boundary strongly typed and independently validated. A single plan represents compound requests without forcing one request into exactly one tool. Classification never executes tools; routing never reads user text; executors never classify. `ExecutionPlan` is consequently suitable as a future Microsoft Agent Framework Workflow message, where workflow edges can dispatch the same envelope to distance, currency, weather, time, or travel-planning executors without redesigning this boundary.

## Validation and one-shot repair

Classification and deterministic validation remain separate. `ExecutionPlanValidator` returns all errors instead of throwing at the first problem and exposes reusable case-insensitive argument helpers used by both validation and routing. `ExecutionPlanProvider` records initial validation, calls `ExecutionPlanRepairAgent` at most once with the original request, invalid plan, and complete error list, then records final validation. The repair agent uses the same structured `ExecutionPlan` response schema; a second invalid result is rejected rather than repaired repeatedly.
