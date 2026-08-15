using SmartTravelPlanner.Api.Models.Execution;

namespace SmartTravelPlanner.Api.Execution;

public sealed class ExecutionTraceScope : IDisposable
{
    private readonly ToolExecutionTraceRecorder _recorder;
    private readonly ToolExecutionTraceRecorder.RequestTrace? _parent;
    private bool _completed;

    internal ExecutionTraceScope(
        ToolExecutionTraceRecorder recorder,
        ToolExecutionTraceRecorder.RequestTrace request,
        ToolExecutionTraceRecorder.RequestTrace? parent)
    {
        _recorder = recorder;
        Request = request;
        _parent = parent;
    }

    internal ToolExecutionTraceRecorder.RequestTrace Request
    {
        get;
    }

    public ExecutionTrace Complete()
    {
        if (_completed)
        {
            throw new InvalidOperationException("The execution trace has already been completed.");
        }

        _completed = true;
        return Request.Complete();
    }

    public void Dispose() => _recorder.EndRequest(this, _parent);
}
