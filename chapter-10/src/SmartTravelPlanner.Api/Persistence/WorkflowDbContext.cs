using Microsoft.EntityFrameworkCore;

namespace SmartTravelPlanner.Api.Persistence;

public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext
(options)
{
    public DbSet<WorkflowRunEntity> WorkflowRuns => Set<WorkflowRunEntity>();
    public DbSet<WorkflowAgentExecutionEntity> AgentExecutions => Set<WorkflowAgentExecutionEntity>();
    public DbSet<WorkflowLiveEventEntity> WorkflowLiveEvents => Set<WorkflowLiveEventEntity>();
    public DbSet<WorkflowExecutorTraceEntity> ExecutorTraces => Set<WorkflowExecutorTraceEntity>();
    public DbSet<WorkflowMessageTransitionEntity> MessageTransitions => Set<WorkflowMessageTransitionEntity>();
    public DbSet<ToolExecutionTraceEntity> ToolTraces => Set<ToolExecutionTraceEntity>();
    public DbSet<ContextProviderTraceEntity> ContextProviderTraces => Set<ContextProviderTraceEntity>();
    public DbSet<ExecutionPlanSnapshotEntity> ExecutionPlans => Set<ExecutionPlanSnapshotEntity>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<WorkflowRunEntity>().HasIndex(x => x.WorkflowRunId).IsUnique();
        foreach (var t in new[] { typeof(WorkflowLiveEventEntity), typeof(WorkflowAgentExecutionEntity),
                                  typeof(WorkflowExecutorTraceEntity), typeof(WorkflowMessageTransitionEntity),
                                  typeof(ToolExecutionTraceEntity), typeof(ContextProviderTraceEntity),
                                  typeof(ExecutionPlanSnapshotEntity) })
            b.Entity(t).HasIndex("WorkflowRunId");
        b.Entity<WorkflowLiveEventEntity>().HasIndex(x => new { x.WorkflowRunId, x.Sequence }).IsUnique();
        b.Entity<WorkflowRunEntity>()
            .HasMany(x => x.AgentExecutions)
            .WithOne()
            .HasForeignKey(x => x.WorkflowRunId)
            .HasPrincipalKey(x => x.WorkflowRunId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<WorkflowRunEntity>()
            .HasMany(x => x.LiveEvents)
            .WithOne()
            .HasForeignKey(x => x.WorkflowRunId)
            .HasPrincipalKey(x => x.WorkflowRunId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<WorkflowRunEntity>()
            .HasMany(x => x.Executors)
            .WithOne()
            .HasForeignKey(x => x.WorkflowRunId)
            .HasPrincipalKey(x => x.WorkflowRunId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<WorkflowRunEntity>()
            .HasMany(x => x.Transitions)
            .WithOne()
            .HasForeignKey(x => x.WorkflowRunId)
            .HasPrincipalKey(x => x.WorkflowRunId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<WorkflowRunEntity>()
            .HasMany(x => x.Tools)
            .WithOne()
            .HasForeignKey(x => x.WorkflowRunId)
            .HasPrincipalKey(x => x.WorkflowRunId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<WorkflowRunEntity>()
            .HasMany(x => x.ContextProviders)
            .WithOne()
            .HasForeignKey(x => x.WorkflowRunId)
            .HasPrincipalKey(x => x.WorkflowRunId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<WorkflowRunEntity>()
            .HasOne(x => x.ExecutionPlan)
            .WithOne()
            .HasForeignKey<ExecutionPlanSnapshotEntity>(x => x.WorkflowRunId)
            .HasPrincipalKey<WorkflowRunEntity>(x => x.WorkflowRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
