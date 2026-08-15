# Chapter 10 — Agent Workflows with Microsoft Agent Framework

## Complete execution-flow reference

For the full request-to-response walkthrough, including typed message transitions, executor boundaries, live events, persistence tables, failure behavior, and diagrams, see [`docs/workflow-execution-flow.md`](docs/workflow-execution-flow.md).


Chapter 10 replaces the application's hand-written request coordinator with a native Microsoft Agent Framework (MAF) `Workflow`. The travel controller now has one dependency, `ITravelWorkflowService`; the service creates and runs a workflow and watches its lifecycle events until a typed `TripPlanResponse` is produced.

## Why a workflow?

By Chapter 9 the application already had classification, validation and repair, deterministic tools, retry/timeout policies, tracing, and a travel agent. What it lacked was an explicit orchestration model. Control flow was hidden inside `TravelAgent`: it called the plan provider and the old execution-plan coordinator before invoking the model.

A MAF workflow makes that control flow data:

```text
TravelWorkflowRequest
        │
ExecutionPlanExecutor
        │ ExecutionPlanMessage
ExecutionPlanValidationExecutor
        │ ValidatedPlanMessage
ToolExecutionExecutor
        │ ToolExecutionMessage
TravelAgentExecutor
        │ TripPlanResponse
```

`TravelPlanningWorkflow` uses `WorkflowBuilder` to declare those four nodes and three ordinary edges. There are intentionally no conditional edges, parallel executors, fan-out/fan-in steps, dynamic routing, approval steps, or shared workflow state in this introductory chapter.

## Why executors?

An executor is a small workflow boundary with one input type and one output type. Each executor has one teaching-friendly responsibility:

* **ExecutionPlanExecutor** calls `IExecutionPlanProvider` to classify the original request.
* **ExecutionPlanValidationExecutor** validates it, performs at most one repair when invalid, validates the repair, and rejects a still-invalid plan.
* **ToolExecutionExecutor** routes and executes validated steps in order. The existing pipeline continues to own retry, timeout, and tool-call tracing behavior.
* **TravelAgentExecutor** gives completed results to `TravelAgent` and asks only for itinerary generation. The agent has no tools and cannot orchestrate or repeat tool work.

## Typed messages

The workflow exchanges records rather than strings or serialized JSON. Each record carries its `WorkflowRunId`, original request, and all accumulated facts needed by the next executor. `ValidatedPlanMessage` adds both validation results and the repaired flag; `ToolExecutionMessage` adds ordered tool results. Downstream nodes never reload those facts from a service, and no shared mutable workflow state is used to transport them.

JSON appears only inside the final natural-language model prompt to render completed tool results; it is not an executor-to-executor transport.

## Workflow lifecycle and events

`TravelWorkflowService` creates a fresh workflow for each HTTP request, awaits an in-process run, observes every collected `WorkflowEvent`, and captures the `WorkflowOutputEvent`. Completion without a `TripPlanResponse` is an error. Exceptions propagate to ASP.NET Core's existing error handler.

The controller cancellation token is passed to the service, workflow runner, each executor, the plan/repair services, tool pipeline, and travel agent. A cancelled run records cancellation and never invokes downstream executors. Tool-level timeouts remain in `ToolExecutionPipeline` and retain the Chapter 9 trace format.

## Observability

Workflow tracing complements tool tracing. Every response contains:

* workflow run ID, start/completion timestamps, duration, status, and failure details;
* executor name, input message type, start/completion timestamps, duration, status, and failure details for every entered executor;
* the existing plan-validation, deterministic tool, retry, timeout, and context-provider trace.

Execution-plan telemetry follows executor ownership: classification timestamps cover only
`ExecutionPlanExecutor`, validation timestamps cover `ExecutionPlanValidationExecutor`, optional repair
timestamps isolate the repair-agent call, and `ToolExecutionDurationMs` covers only deterministic tool execution.

Logs mark workflow start/completion/failure/cancellation and executor start/completion. The tool pipeline continues logging tool selection, retries, timeouts, completion, and failures.

## Run the chapter

```bash
dotnet restore SmartTravelPlanner.sln
dotnet build SmartTravelPlanner.sln --no-restore
dotnet test SmartTravelPlanner.sln --no-build
```

Configure `AzureOpenAI` values in user secrets or environment variables before making a live request. Then POST a `TravelPlanRequest` to `/api/travel/plan`.

## Blazor Workflow Explorer

The reusable `SmartTravelPlanner.Client` WebAssembly application opens on a single Plan a Trip page. It starts one live travel-planning workflow, navigates to the live run view, and exposes executor order, typed message transitions, agent activity, individual tool calls, context providers, proportional timing, and the final itinerary without a scenario dropdown or extra chapter navigation pages.

Run the API and client in separate terminals:

```bash
dotnet run --project src/SmartTravelPlanner.Api
dotnet run --project src/SmartTravelPlanner.Client
```

The expected development URLs are `https://localhost:54918` (API) and `https://localhost:7182` (client). The client reads `Api:BaseUrl` from `wwwroot/appsettings.json`, with an environment-specific override in `appsettings.Development.json`; components contain no endpoint URL. The API reads `Cors:AllowedOrigins` and permits only the configured client origins. `GET /health` powers the startup/manual connectivity check without polling.

The default request is Jaipur for three days with weather, currency conversion, and Hyderabad-distance preferences. The live run page displays the workflow topology immediately, then applies persisted and SignalR-delivered events in sequence. Raw JSON stays behind expandable details. Sensitive snapshots are omitted; safe diagnostic DTOs—not internal MAF message instances—cross the HTTP boundary.

The shell and diagnostic contracts are intended to evolve with later chapters covering memory, MCP, branching and parallel workflows, human approval, persistence, and production observability.

## Historical workflow runs and comparison

Chapter 10 persists diagnostic workflow checkpoints to the configured SQLite database (`ConnectionStrings:WorkflowRuns`) so a reader can return to earlier executions, study failures even when a downstream executor did not complete, and compare how plans, tools, retries, context providers, and duration change between requests. `WorkflowPersistence:RetentionDays` limits list visibility and `PersistDiagnosticPayloads` controls safe input/output and trip-plan JSON capture. No cleanup scheduler is included yet; a later observability chapter will add retention maintenance.

The UI links directly from the request form to `/workflow-runs/{id}/live`. The API still exposes run details and event replay under `/api/workflow-runs`, but the chapter no longer includes separate list, comparison, or about pages in the Blazor client.

Stored data includes the run envelope, ordered executor boundaries, redacted typed-transition summaries, distinct tool invocations, context-provider metadata, validation/repair plans, and the final trip plan when payload persistence is enabled. Internal MAF messages, system prompts, credentials, authorization headers, connection strings, and private traveler-memory values are deliberately not stored. Persistence failures are logged and isolated from workflow execution.

## Live Workflow Streaming

Chapter 10 now keeps the synchronous `POST /api/travel/plan` endpoint for callers that only need the final `TripPlanResponse`. That response cannot show executor activity while the request is still running because HTTP returns a single final payload after the Microsoft Agent Framework workflow completes.

The Blazor Workflow Explorer starts live runs with `POST /api/workflow-runs`, receives a `WorkflowRunId` immediately, subscribes to `/hubs/workflow-events`, replays persisted events from `GET /api/workflow-runs/{workflowRunId}/events`, and then applies SignalR updates as they arrive. SignalR is used only as the immediate transport; persisted `WorkflowLiveEvents` are the source of truth so reconnecting clients can reconstruct missed executor, agent, tool, and message-transition updates.

Executors are workflow stages (`ExecutionPlanExecutor`, `ExecutionPlanValidationExecutor`, `ToolExecutionExecutor`, `TravelAgentExecutor`). Agents are invoked by stages, such as the repair agent during validation or the Travel Agent during final itinerary generation. The UI displays safe progress summaries and never exposes private reasoning or chain-of-thought.

The live queue is intentionally in-process for this learning chapter. Durable distributed background execution belongs in a later production chapter.

### Application agent results vs. MAF SDK responses

MAF supplies the agent execution primitive (`AgentResponse<T>`), but Chapter 10 uses an application-level `AgentResult<T>` at workflow boundaries. The SDK response stays inside `TravelAgent`; the workflow receives a typed success/failure result with safe metadata. This distinction lets the sample teach that typed output is still validated, expected AI failures are different from infrastructure exceptions, and workflows—not agents—own sequencing, retries, regeneration, and stop decisions.

`TravelAgentRequest` packages the original user request, validated travel request, tool results, and optional context into a single typed input. If structured output cannot be read or deterministic validation fails, the result contains an `AgentFailure` and `AgentExecutionMetadata` rather than an unclassified generic exception. The Workflow Explorer surfaces safe agent lifecycle events such as started, completed, and failed without storing raw prompts or full model responses by default.
