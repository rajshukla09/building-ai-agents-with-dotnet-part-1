using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartTravelPlanner.Api.Models.Execution;
using SmartTravelPlanner.Api.Persistence;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class WorkflowRunStoreTests : IDisposable
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"workflow-{Guid.NewGuid():N}.db");
    private readonly TestFactory _factory;
    private readonly EfWorkflowRunStore _store;

    public WorkflowRunStoreTests()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>().UseSqlite($"Data Source={_db}").Options;
        _factory = new(options);
        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();
        _store =
            new(_factory, Options.Create(new WorkflowPersistenceOptions()), NullLogger<EfWorkflowRunStore>.Instance);
    }

    [Fact]
    public async Task Persists_filters_compares_and_deletes_runs()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await Add(a, "Jaipur", false, 100);
        await Add(b, "Jaipur", true, 175);
        var list = await _store.ListAsync(new(Destination: "Jaip", RepairAttempted: true));
        Assert.Single(list.Items);
        var detail = await _store.GetAsync(b);
        Assert.NotNull(detail);
        Assert.Equal(2, detail!.Tools.Count);
        var comparison = await _store.CompareAsync(a, b);
        Assert.Equal(75, comparison!.TotalDurationDifferenceMs);
        Assert.True(comparison.RepairDiffers);
        Assert.True(await _store.DeleteAsync(a));
        Assert.Null(await _store.GetAsync(a));
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Cancelled")]
    public async Task Terminal_failure_states_are_persisted(string status)
    {
        var id = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow;
        await _store.StartAsync(id, new("Jaipur", 3), "safe", start);
        await _store.CompleteAsync(id, status, start.AddMilliseconds(5), status, "safe error");
        Assert.Equal(status, (await _store.GetAsync(id))!.Status);
    }

    [Fact]
    public async Task Persistence_failure_is_isolated()
    {
        var broken = new EfWorkflowRunStore(new ThrowingFactory(), Options.Create(new WorkflowPersistenceOptions()),
                                            NullLogger<EfWorkflowRunStore>.Instance);
        await broken.StartAsync(Guid.NewGuid(), new("Jaipur", 3), "safe", DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Sensitive_values_are_never_stored()
    {
        var id = Guid.NewGuid();
        await _store.StartAsync(id, new("Jaipur", 3), "authorization: Bearer secret", DateTimeOffset.UtcNow);
        await _store.CompleteAsync(id, "Failed", DateTimeOffset.UtcNow, "Agent", "apiKey=secret");
        var run = await _store.GetAsync(id);
        Assert.Equal("[REDACTED]", run!.OriginalRequest);
        Assert.Equal("[REDACTED]", run.Error);
    }

    private async Task Add(Guid id, string destination, bool repair, long duration)
    {
        var start = DateTimeOffset.UtcNow;
        await _store.StartAsync(id, new(destination, 3), "safe request", start);
        await _store.RecordExecutorAsync(
            new() { WorkflowRunId = id, ExecutorName = "ExecutionPlanExecutor", MessageType = "TravelWorkflowRequest",
                    OutputMessageType = "ExecutionPlanMessage", StartedAt = start,
                    CompletedAt = start.AddMilliseconds(10), DurationMs = 10, Status = "Completed" });
        var trace = new ExecutionTrace {
            StartedAt = start,
            CompletedAt = start.AddMilliseconds(duration),
            TotalDurationMs = duration,
            ToolCalls = [Tool(1), Tool(2)],
            ContextProviders = [],
            ExecutionPlan = new() { ClassificationStartedAt = start, ClassificationCompletedAt = start,
                                    ClassificationDurationMs = 0, InitialValidationSucceeded = !repair,
                                    InitialValidationErrors = repair ? ["bad argument"] : [], RepairAttempted = repair,
                                    RepairedPlan = null, FinalValidationSucceeded = true, FinalValidationErrors = [] }
        };
        await _store.SaveDiagnosticsAsync(id, trace, new { summary = "trip" });
        await _store.CompleteAsync(id, "Completed", start.AddMilliseconds(duration));
    }

    private static ToolExecution Tool(int order) => new() { Order = order,
                                                            PlanStepOrder = order,
                                                            ToolName = "Weather",
                                                            InvocationMode = "Workflow",
                                                            StartedAt = DateTimeOffset.UtcNow,
                                                            CompletedAt = DateTimeOffset.UtcNow,
                                                            DurationMs = 1,
                                                            Status = "Success",
                                                            RetryCount = 0,
                                                            Timeout = false

    };
    public void Dispose()

    {
        _factory.Dispose();
        try
        {
            File.Delete(_db);
        }
        catch
        {
        }
    }

    private sealed class ThrowingFactory : IDbContextFactory<WorkflowDbContext>
    {
        public WorkflowDbContext CreateDbContext() => throw new InvalidOperationException("database unavailable");
        public Task<WorkflowDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("database unavailable");
    }

    private sealed class TestFactory(DbContextOptions<WorkflowDbContext> options)
        : IDbContextFactory<WorkflowDbContext>, IDisposable
    {
        public WorkflowDbContext CreateDbContext() => new(options);
        public Task<WorkflowDbContext>
        CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
        public void Dispose()

        {
        }
    }
}
