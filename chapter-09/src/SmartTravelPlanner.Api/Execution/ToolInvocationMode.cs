namespace SmartTravelPlanner.Api.Execution;

public enum ToolInvocationMode { Deterministic, ModelSelected }

public sealed class TransientToolException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class ToolExecutionFailedException(string toolName, string message, Exception? innerException = null)
    : Exception($"{toolName} could not complete: {message}", innerException);
