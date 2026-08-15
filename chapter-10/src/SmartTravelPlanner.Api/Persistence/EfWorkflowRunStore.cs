using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SmartTravelPlanner.Api.Agents.Results;
using Microsoft.Extensions.Options;
using SmartTravelPlanner.Contracts;
using ApiExecutionTrace = SmartTravelPlanner.Api.Models.Execution.ExecutionTrace;
using ApiExecutorExecutionTrace = SmartTravelPlanner.Api.Models.Execution.ExecutorExecutionTrace;
using ContractWorkflowMessageTransitionDto = SmartTravelPlanner.Contracts.WorkflowMessageTransitionDto;
using ContractWorkflowValueChangeDto = SmartTravelPlanner.Contracts.WorkflowValueChangeDto;

namespace SmartTravelPlanner.Api.Persistence;

public sealed class EfWorkflowRunStore(IDbContextFactory<WorkflowDbContext> factory,
                                       IOptions<WorkflowPersistenceOptions> options, ILogger<EfWorkflowRunStore> logger)
    : IWorkflowRunStore
{
    private static readonly JsonSerializerOptions Json = CreateJsonOptions();
    private readonly WorkflowPersistenceOptions _options = options.Value;

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public async Task StartAsync(Guid id, TravelPlanRequest request, string original, DateTimeOffset started,
                                 CancellationToken ct = default) =>
        await Safe(async () =>
                   {
                       await using var db = await factory.CreateDbContextAsync(ct);
                       db.WorkflowRuns.Add(new() { WorkflowRunId = id, Destination = request.Destination,
                                                   DurationDays = request.DurationDays,
                                                   OriginalRequest = RedactText(original), StartedAt = started,
                                                   CreatedAt = started });
                       await db.SaveChangesAsync(ct);
                   });

    public async Task RecordExecutorAsync(ApiExecutorExecutionTrace x, CancellationToken ct = default) => await Safe(
        async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            if (await db.ExecutorTraces.AnyAsync(
                    e => e.WorkflowRunId == x.WorkflowRunId && e.Order > 0 && e.ExecutorName == x.ExecutorName, ct))
                return;
            var order = await db.ExecutorTraces.CountAsync(e => e.WorkflowRunId == x.WorkflowRunId, ct) + 1;
            db.ExecutorTraces.Add(new() { WorkflowRunId = x.WorkflowRunId, Order = order, ExecutorName = x.ExecutorName,
                                          InputMessageType = x.MessageType,
                                          OutputMessageType = x.OutputMessageType ?? "Unknown", StartedAt = x.StartedAt,
                                          CompletedAt = x.CompletedAt, DurationMs = x.DurationMs, Status = x.Status,
                                          Exception = RedactText(x.Exception) });
            db.MessageTransitions.Add(
                new() { WorkflowRunId = x.WorkflowRunId, Order = order, ExecutorName = x.ExecutorName,
                        InputMessageType = x.MessageType, OutputMessageType = x.OutputMessageType ?? "Unknown",
                        CarriedForwardJson = Serialize(new { workflowRunId = x.WorkflowRunId }),
                        AddedJson = Serialize(new { stageCompleted = x.Status }), ChangedJson = "{}" });
            await db.SaveChangesAsync(ct);
        });

    public async Task RecordAgentExecutionAsync(Guid runId, string executorName, AgentFailure? failure,
                                                AgentExecutionMetadata metadata, CancellationToken ct = default) =>
        await Safe(async () =>
                   {
                       await using var db = await factory.CreateDbContextAsync(ct);
                       db.AgentExecutions.Add(
                           new() { WorkflowRunId = runId, ExecutorName = executorName, AgentName = metadata.AgentName,
                                   RequestedResponseType = metadata.RequestedResponseType, IsSuccess = failure is null,
                                   FailureKind = failure?.Kind.ToString(), FailureCode = failure?.Code,
                                   FailurePath = failure?.Path, Retryable = failure?.Retryable ?? false,
                                   AttemptCount = metadata.AttemptCount,
                                   StructuredDeserializationSucceeded = metadata.StructuredDeserializationSucceeded,
                                   RawRecoveryAttempted = metadata.RawRecoveryAttempted,
                                   RawRecoverySucceeded = metadata.RawRecoverySucceeded,
                                   RegenerationAttempted = metadata.RegenerationAttempted,
                                   RegenerationSucceeded = metadata.RegenerationSucceeded,
                                   DurationMs = metadata.DurationMs, FinalStatus = metadata.Status.ToString(),
                                   WarningsJson = Serialize(metadata.Warnings) });
                       await db.SaveChangesAsync(ct);
                   });

    public async Task SaveDiagnosticsAsync(Guid id, ApiExecutionTrace trace, object? tripPlan,
                                           CancellationToken ct = default) =>
        await Safe(async () =>
                   {
                       await using var db = await factory.CreateDbContextAsync(ct);
                       var run = await db.WorkflowRuns.FirstOrDefaultAsync(x => x.WorkflowRunId == id, ct);
                       if (run is null)
                           return;
                       run.TripPlanJson =
                           _options.PersistDiagnosticPayloads && tripPlan is not null ? Serialize(tripPlan) : null;
                       foreach (var x in trace.ToolCalls.Where(
                                    x => !db.ToolTraces.Any(e => e.WorkflowRunId == id && e.Order == x.Order)))
                           db.ToolTraces.Add(
                               new() { WorkflowRunId = id, Order = x.Order, PlanStepOrder = x.PlanStepOrder,
                                       ToolName = x.ToolName, InvocationMode = x.InvocationMode,
                                       InputJson = _options.PersistDiagnosticPayloads ? SafeSerialize(x.Input) : null,
                                       OutputJson = _options.PersistDiagnosticPayloads ? SafeSerialize(x.Output) : null,
                                       StartedAt = x.StartedAt, CompletedAt = x.CompletedAt, DurationMs = x.DurationMs,
                                       Status = x.Status, RetryCount = x.RetryCount, Timeout = x.Timeout,
                                       Error = RedactText(x.Error), FailureReason = RedactText(x.FailureReason) });
                       foreach (var x in trace.ContextProviders.Where(
                                    x => !db.ContextProviderTraces.Any(e => e.WorkflowRunId == id &&
                                                                            e.Order == x.Order)))
                           db.ContextProviderTraces.Add(
                               new() { WorkflowRunId = id, Order = x.Order, ProviderName = x.ProviderName,
                                       ContextCategory = x.ContextCategory, DurationMs = x.DurationMs,
                                       ContextAdded = x.ContextAdded, Status = x.Status,
                                       SafeContextSummary =
                                           x.ContextAdded ? "Context contributed; private values omitted." : null });
                       if (trace.ExecutionPlan is { } p)
                       {
                           var entity = await db.ExecutionPlans.FirstOrDefaultAsync(x => x.WorkflowRunId == id, ct) ??
                                        new() { WorkflowRunId = id };
                           entity.InitialPlanJson = SafeSerialize(p.InitialPlan);
                           entity.InitialValidationSucceeded = p.InitialValidationSucceeded;
                           entity.InitialValidationErrorsJson = Serialize(p.InitialValidationErrors);
                           entity.RepairAttempted = p.RepairAttempted;
                           entity.RepairedPlanJson = SafeSerialize(p.RepairedPlan);
                           entity.FinalValidationSucceeded = p.FinalValidationSucceeded;
                           entity.FinalValidationErrorsJson = Serialize(p.FinalValidationErrors);
                           entity.GeneratedPlanJson = SafeSerialize(p.GeneratedPlan);
                           run.RepairAttempted = p.RepairAttempted;
                           if (entity.Id == 0)
                               db.ExecutionPlans.Add(entity);
                       }
                       await db.SaveChangesAsync(ct);
                   });

    public async Task CompleteAsync(Guid id, string status, DateTimeOffset completed, string? stage = null,
                                    string? error = null, CancellationToken ct = default) =>
        await Safe(async () =>
                   {
                       await using var db = await factory.CreateDbContextAsync(ct);
                       var x = await db.WorkflowRuns.FirstOrDefaultAsync(x => x.WorkflowRunId == id, ct);
                       if (x is null)
                           return;
                       x.Status = status;
                       x.CompletedAt = completed;
                       x.DurationMs = Math.Max(0, (long)(completed - x.StartedAt).TotalMilliseconds);
                       x.FailureStage = stage;
                       x.Error = RedactText(error);
                       await db.SaveChangesAsync(ct);
                   });

    public async Task<PagedWorkflowRunsDto> ListAsync(WorkflowRunQuery q, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        DateTimeOffset retentionCutoff = DateTimeOffset.UtcNow.AddDays(-_options.RetentionDays);
        var query = db.WorkflowRuns.AsNoTracking().Where(x => x.CreatedAt >= retentionCutoff);
        if (!string.IsNullOrWhiteSpace(q.Status))
            query = query.Where(x => x.Status == q.Status);
        if (!string.IsNullOrWhiteSpace(q.Destination))
            query = query.Where(x => x.Destination.Contains(q.Destination));
        if (q.RepairAttempted.HasValue)
            query = query.Where(x => x.RepairAttempted == q.RepairAttempted);
        if (q.From.HasValue)
            query = query.Where(x => x.StartedAt >= q.From);
        if (q.To.HasValue)
            query = query.Where(x => x.StartedAt <= q.To);
        int size = Math.Clamp(q.PageSize, 1, 100), page = Math.Max(1, q.Page), total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.StartedAt)
                        .Skip((page - 1) * size)
                        .Take(size)
                        .Select(x => new WorkflowRunSummaryDto(x.WorkflowRunId, x.Destination, x.Status, x.StartedAt,
                                                               x.DurationMs, x.RepairAttempted, x.Executors.Count,
                                                               x.Tools.Count))
                        .ToListAsync(ct);
        return new(items, page, size, total);
    }

    public async Task<WorkflowRunDetailsDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var x = await db.WorkflowRuns.AsNoTracking()
                    .Include(x => x.Executors)
                    .Include(x => x.Transitions)
                    .Include(x => x.Tools)
                    .Include(x => x.ContextProviders)
                    .Include(x => x.ExecutionPlan)
                    .SingleOrDefaultAsync(x => x.WorkflowRunId == id, ct);
        return x is null ? null : Map(x);
    }

    public async Task<WorkflowRunComparisonDto?> CompareAsync(Guid a, Guid b, CancellationToken ct = default)
    {
        var aa = await GetAsync(a, ct);
        var bb = await GetAsync(b, ct);
        if (aa is null || bb is null)
            return null;
        var sa = Summary(aa);
        var sb = Summary(bb);
        var names = aa.Executors.Select(x => x.ExecutorName).Union(bb.Executors.Select(x => x.ExecutorName));
        var durations = names
                            .Select(n =>
                                    {
                                        var av = aa.Executors.FirstOrDefault(x => x.ExecutorName == n)?.DurationMs;
                                        var bv = bb.Executors.FirstOrDefault(x => x.ExecutorName == n)?.DurationMs;
                                        return new NamedDurationDifferenceDto(n, av, bv, (bv ?? 0) - (av ?? 0));
                                    })
                            .ToArray();
        return new(sa, sb, bb.DurationMs - aa.DurationMs, durations,
                   Diff(aa.Tools.Select(x => $"{x.Order}:{x.ToolName}:{x.Status}:{x.RetryCount}"),
                        bb.Tools.Select(x => $"{x.Order}:{x.ToolName}:{x.Status}:{x.RetryCount}")),
                   aa.RepairAttempted != bb.RepairAttempted,
                   Diff([aa.ExecutionPlan?.GeneratedPlanJson ?? ""], [bb.ExecutionPlan?.GeneratedPlanJson ?? ""]),
                   Diff(aa.ContextProviders.Select(x => $"{x.Order}:{x.ProviderName}:{x.Status}"),
                        bb.ContextProviders.Select(x => $"{x.Order}:{x.ProviderName}:{x.Status}")),
                   aa.Status != bb.Status, SummaryText(aa.TripPlanJson) != SummaryText(bb.TripPlanJson));
    }

    public async Task RecordLiveEventAsync(WorkflowLiveEventDto e, string? safeDataJson,
                                           CancellationToken ct = default) =>
        await Safe(async () =>
                   {
                       await using var db = await factory.CreateDbContextAsync(ct);
                       if (await db.WorkflowLiveEvents.AnyAsync(
                               x => x.WorkflowRunId == e.WorkflowRunId && x.Sequence == e.Sequence, ct))
                           return;
                       db.WorkflowLiveEvents.Add(
                           new() { WorkflowRunId = e.WorkflowRunId, Sequence = e.Sequence, OccurredAt = e.OccurredAt,
                                   EventType = e.EventType.ToString(), StageType = e.StageType.ToString(),
                                   StageName = e.StageName, Status = e.Status.ToString(),
                                   InputMessageType = e.InputMessageType, OutputMessageType = e.OutputMessageType,
                                   DurationMs = e.DurationMs, Summary = RedactText(e.Summary),
                                   SafeDataJson = safeDataJson });
                       await db.SaveChangesAsync(ct);
                   });

    public async Task<IReadOnlyList<WorkflowLiveEventDto>>
    GetLiveEventsAsync(Guid workflowRunId, long afterSequence = 0, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.WorkflowLiveEvents.AsNoTracking()
                       .Where(x => x.WorkflowRunId == workflowRunId && x.Sequence > afterSequence)
                       .OrderBy(x => x.Sequence)
                       .ToListAsync(ct);
        return rows
            .Select(x => new WorkflowLiveEventDto(x.WorkflowRunId, x.Sequence, x.OccurredAt,
                                                  Enum.Parse<WorkflowLiveEventType>(x.EventType),
                                                  Enum.Parse<WorkflowStageType>(x.StageType), x.StageName,
                                                  Enum.Parse<WorkflowStageStatus>(x.Status), x.InputMessageType,
                                                  x.OutputMessageType, x.DurationMs, x.Summary,
                                                  string.IsNullOrWhiteSpace(x.SafeDataJson)
                                                      ? null
                                                      : JsonSerializer.Deserialize<object>(x.SafeDataJson, Json)))
            .ToArray();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var x = await db.WorkflowRuns.SingleOrDefaultAsync(x => x.WorkflowRunId == id, ct);
        if (x is null)
            return false;
        db.Remove(x);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task Safe(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow persistence checkpoint failed; workflow execution continues");
        }
    }

    private static string Serialize(object? x) => JsonSerializer.Serialize(x, Json);
    private static string? SafeSerialize(object? x) => x is null ? null : RedactText(Serialize(x));
    private static string? RedactText(string? s)

    {
        if (s is null)
            return null;
        foreach (var key in new[] { "apikey", "authorization", "connectionstring", "systemprompt", "secret",
                                    "traveler-memory" })
            if (s.Contains(key, StringComparison.OrdinalIgnoreCase))
                return "[REDACTED]";
        return s;
    }

    private static WorkflowRunSummaryDto Summary(WorkflowRunDetailsDto x) => new(x.WorkflowRunId, x.Destination,
                                                                                 x.Status, x.StartedAt, x.DurationMs,
                                                                                 x.RepairAttempted, x.Executors.Count,
                                                                                 x.Tools.Count);
    private static IReadOnlyList<string> Diff(IEnumerable<string> a, IEnumerable<string> b) =>
        a.SequenceEqual(b) ? []
                           : [..a.Except(b).Select(x => "Only A: " + x), ..b.Except(a).Select(x => "Only B: " + x)];
    private static string SummaryText(string? json) => json is null ? "" : json.Length <= 200 ? json : json[..200];

    private static WorkflowRunDetailsDto Map(WorkflowRunEntity x) => new(
        x.WorkflowRunId, x.Destination, x.DurationDays, x.OriginalRequest, x.Status, x.StartedAt, x.CompletedAt,
        x.DurationMs, x.RepairAttempted, x.FailureStage, x.Error, x.TripPlanJson,
        x.Executors.OrderBy(e => e.Order)
            .Select(e => new PersistedExecutorDto(e.Order, e.ExecutorName, e.InputMessageType, e.OutputMessageType,
                                                  e.StartedAt, e.CompletedAt, e.DurationMs, e.Status, e.Exception))
            .ToArray(),
        x.Transitions.OrderBy(e => e.Order)
            .Select(e => new ContractWorkflowMessageTransitionDto(
                        e.Order, e.ExecutorName, e.InputMessageType, e.OutputMessageType,
                        Deserialize(e.CarriedForwardJson), Deserialize(e.AddedJson),
                        new Dictionary<string, ContractWorkflowValueChangeDto>(), null, null))
            .ToArray(),
        x.Tools.OrderBy(e => e.Order)
            .Select(e => new PersistedToolDto(e.Order, e.PlanStepOrder, e.ToolName, e.InvocationMode, e.InputJson,
                                              e.OutputJson, e.StartedAt, e.CompletedAt, e.DurationMs, e.Status,
                                              e.RetryCount, e.Timeout, e.Error, e.FailureReason))
            .ToArray(),
        x.ContextProviders.OrderBy(e => e.Order)
            .Select(e => new PersistedContextProviderDto(e.Order, e.ProviderName, e.ContextCategory, e.DurationMs,
                                                         e.ContextAdded, e.Status, e.SafeContextSummary))
            .ToArray(),
        x.ExecutionPlan is null ? null
                                : new(x.ExecutionPlan.InitialPlanJson, x.ExecutionPlan.InitialValidationSucceeded,
                                      x.ExecutionPlan.InitialValidationErrorsJson, x.ExecutionPlan.RepairAttempted,
                                      x.ExecutionPlan.RepairedPlanJson, x.ExecutionPlan.FinalValidationSucceeded,
                                      x.ExecutionPlan.FinalValidationErrorsJson, x.ExecutionPlan.GeneratedPlanJson));
    private static IReadOnlyDictionary<string, object?>
    Deserialize(string json) => JsonSerializer.Deserialize<Dictionary<string, object?>>(json, Json) ?? new();
}
