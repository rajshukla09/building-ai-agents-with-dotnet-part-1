using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class StreamingUiTests
{
    private static readonly string ProjectDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SmartTravelPlanner.Api"));

    [Fact]
    public void UiReadsAndRendersUpdatesAsTheyArrive()
    {
        string script = File.ReadAllText(Path.Combine(ProjectDirectory, "wwwroot", "js", "app.js"));

        Assert.Contains("response.body.getReader()", script);
        Assert.Contains("await reader.read()", script);
        Assert.Contains("assistantMessage.textContent += update.delta", script);
        Assert.DoesNotContain("await response.text()", script);
    }

    [Fact]
    public void GeneratingThenStopBecomesCancelledWithoutTreatingAbortAsFailure()
    {
        string script = File.ReadAllText(Path.Combine(ProjectDirectory, "wwwroot", "js", "app.js"));
        string page = File.ReadAllText(Path.Combine(ProjectDirectory, "wwwroot", "index.html"));

        Assert.Contains("new AbortController()", script);
        Assert.Contains("setStatus('generating')", script);
        Assert.Contains("request.reader = reader", script);
        Assert.Contains("activeRequest.cancelled = true", script);
        Assert.Contains("activeRequest.reader.cancel()", script);
        Assert.Contains("activeRequest.controller.abort()", script);
        Assert.Contains("if (request.cancelled)", script);
        Assert.Contains("setStatus('cancelled')", script);
        Assert.DoesNotContain("error.name === 'AbortError'", script);
        Assert.Contains("if (activeRequest === request) activeRequest = undefined", script);
        Assert.Contains("sendButton.disabled = false", script);
        Assert.Contains("id=\"stop\"", page);
        Assert.Contains("Generating", page + script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Completed", page + script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Failed", page + script, StringComparison.OrdinalIgnoreCase);
    }
}
