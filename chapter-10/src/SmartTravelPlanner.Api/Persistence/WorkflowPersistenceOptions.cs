using System.ComponentModel.DataAnnotations;

namespace SmartTravelPlanner.Api.Persistence;

public sealed class WorkflowPersistenceOptions
{
    public const string SectionName = "WorkflowPersistence";
    [Range(1, 3650)] public int RetentionDays { get; init; } = 30;
    public bool PersistDiagnosticPayloads { get; init; } = true;
}
