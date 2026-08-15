using SmartTravelPlanner.Api.Models.Execution;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class WorkflowDiagnosticTests
{
    [Fact]
    public void Redactor_removes_sensitive_diagnostic_values()
    {
        var safe = DiagnosticRedactor.Redact(new Dictionary<string, object?> { ["apiKey"] = "secret-value", ["destination"] = "Jaipur", ["Authorization"] = "Bearer token" });
        Assert.Equal("[REDACTED]", safe["apiKey"]);
        Assert.Equal("[REDACTED]", safe["Authorization"]);
        Assert.Equal("Jaipur", safe["destination"]);
    }

    [Fact]
    public void Repeated_tool_calls_remain_separate_rows()
    {
        ToolExecution[] calls = [Tool(1, "Currency"), Tool(2, "Currency")];
        Assert.Equal(2, calls.OrderBy(call => call.Order).Count());
        Assert.NotEqual(calls[0].Order, calls[1].Order);
    }

    private static ToolExecution Tool(int order, string name) => new() { Order = order,
                                                                         ToolName = name,
                                                                         InvocationMode = "Workflow",
                                                                         StartedAt = DateTimeOffset.UtcNow,
                                                                         CompletedAt = DateTimeOffset.UtcNow,
                                                                         DurationMs = 1,
                                                                         Status = "Success",
                                                                         RetryCount = 0,
                                                                         Timeout = false };
}
