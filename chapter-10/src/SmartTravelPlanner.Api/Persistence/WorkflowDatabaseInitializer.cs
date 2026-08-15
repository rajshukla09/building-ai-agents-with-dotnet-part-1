using Microsoft.EntityFrameworkCore;

namespace SmartTravelPlanner.Api.Persistence;

public static class WorkflowDatabaseInitializer
{
    public static async Task EnsureReadyAsync(WorkflowDbContext db, CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);

        if (!db.Database.IsSqlite())
            return;

        await db.Database.OpenConnectionAsync(ct);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS "AgentExecutions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_AgentExecutions" PRIMARY KEY AUTOINCREMENT,
                "WorkflowRunId" TEXT NOT NULL,
                "ExecutorName" TEXT NOT NULL,
                "AgentName" TEXT NOT NULL,
                "RequestedResponseType" TEXT NOT NULL,
                "IsSuccess" INTEGER NOT NULL,
                "FailureKind" TEXT NULL,
                "FailureCode" TEXT NULL,
                "FailurePath" TEXT NULL,
                "Retryable" INTEGER NOT NULL,
                "AttemptCount" INTEGER NOT NULL,
                "StructuredDeserializationSucceeded" INTEGER NOT NULL,
                "RawRecoveryAttempted" INTEGER NOT NULL,
                "RawRecoverySucceeded" INTEGER NOT NULL,
                "RegenerationAttempted" INTEGER NOT NULL,
                "RegenerationSucceeded" INTEGER NOT NULL,
                "DurationMs" INTEGER NOT NULL,
                "FinalStatus" TEXT NOT NULL,
                "WarningsJson" TEXT NOT NULL,
                CONSTRAINT "FK_AgentExecutions_WorkflowRuns_WorkflowRunId" FOREIGN KEY ("WorkflowRunId") REFERENCES "WorkflowRuns" ("WorkflowRunId") ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS "IX_AgentExecutions_WorkflowRunId" ON "AgentExecutions" ("WorkflowRunId");

            CREATE TABLE IF NOT EXISTS "WorkflowLiveEvents" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_WorkflowLiveEvents" PRIMARY KEY AUTOINCREMENT,
                "WorkflowRunId" TEXT NOT NULL,
                "Sequence" INTEGER NOT NULL,
                "OccurredAt" TEXT NOT NULL,
                "EventType" TEXT NOT NULL,
                "StageType" TEXT NOT NULL,
                "StageName" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "InputMessageType" TEXT NULL,
                "OutputMessageType" TEXT NULL,
                "DurationMs" INTEGER NULL,
                "Summary" TEXT NULL,
                "SafeDataJson" TEXT NULL,
                CONSTRAINT "FK_WorkflowLiveEvents_WorkflowRuns_WorkflowRunId" FOREIGN KEY ("WorkflowRunId") REFERENCES "WorkflowRuns" ("WorkflowRunId") ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS "IX_WorkflowLiveEvents_WorkflowRunId" ON "WorkflowLiveEvents" ("WorkflowRunId");

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_WorkflowLiveEvents_WorkflowRunId_Sequence" ON "WorkflowLiveEvents" ("WorkflowRunId", "Sequence");
            """;
        await command.ExecuteNonQueryAsync(ct);
    }
}
