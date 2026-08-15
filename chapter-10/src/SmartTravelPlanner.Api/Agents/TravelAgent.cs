using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using SmartTravelPlanner.Api.Agents.Results;
using SmartTravelPlanner.Api.Configuration;
using SmartTravelPlanner.Api.Context;
using SmartTravelPlanner.Api.Conversations;
using SmartTravelPlanner.Api.Execution;
using SmartTravelPlanner.Api.Models.Execution;
using SmartTravelPlanner.Api.Models.TravelPlanning;

using TripPlanResponse = SmartTravelPlanner.Api.Models.Execution.TripPlanResponse;

namespace SmartTravelPlanner.Api.Agents;

public sealed class TravelAgent : ITravelAgent, IAgentSessionSerializer
{
    private const int MaximumAttempts = 2;
    private readonly AIAgent _agent;
    private readonly ILogger<TravelAgent> _logger;
    private readonly IExecutionTraceRecorder _traceRecorder;
    private readonly TravelInvocationContextAccessor _contextAccessor;
    private readonly IAgentFailurePolicy _failurePolicy;
    private readonly TimeProvider _timeProvider;

    public TravelAgent(
        IOptions<AzureOpenAIOptions> options,
        ILogger<TravelAgent> logger,
        IExecutionTraceRecorder traceRecorder,
        TravelInvocationContextAccessor contextAccessor,
        TravelerMemoryContextProvider memoryContextProvider,
        RuntimeTravelContextProvider runtimeContextProvider,
        IAgentFailurePolicy failurePolicy,
        TimeProvider timeProvider)
    {
        AzureOpenAIOptions settings = options.Value;
        _logger = logger;
        _traceRecorder = traceRecorder;
        _contextAccessor = contextAccessor;
        _failurePolicy = failurePolicy;
        _timeProvider = timeProvider;
        _agent = new AzureOpenAIClient(new Uri(settings.Endpoint), new AzureKeyCredential(settings.ApiKey))
            .GetChatClient(settings.DeploymentName)
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = nameof(TravelAgent),
                ChatOptions = new ChatOptions
                {
                    Instructions = TravelAgentInstructions.SystemPrompt
                },
                AIContextProviders = [memoryContextProvider, runtimeContextProvider]
            });
    }

    public async Task<AgentResult<TripPlan>> ExecuteAsync(
        TravelAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using IDisposable invocation = _contextAccessor.Push(new TravelInvocationContext(
            Destination: request.TravelRequest.Destination,
            DurationDays: request.TravelRequest.DurationDays));

        DateTimeOffset startedAt = _timeProvider.GetUtcNow();
        List<string> warnings = [];
        AgentFailure? lastFailure = null;
        bool rawRecoveryAttempted = false;
        bool rawRecoverySucceeded = false;
        bool regenerationAttempted = false;
        bool regenerationSucceeded = false;
        bool structuredSucceeded = false;

        for (int attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string prompt = attempt == 1 ? CreatePrompt(request) : CreateRegenerationPrompt(request, lastFailure);
            if (attempt > 1)
            {
                regenerationAttempted = true;
            }

            AgentResponse<TripPlan> response = await _agent.RunAsync<TripPlan>(
                prompt,
                options: CreateStructuredOutputOptions(),
                cancellationToken: cancellationToken);

            StructuredReadResult read = ReadStructuredResult(() => response.Result, response.Text, _logger);
            rawRecoveryAttempted |= read.RawRecoveryAttempted;
            rawRecoverySucceeded |= read.RawRecoverySucceeded;
            structuredSucceeded |= read.StructuredDeserializationSucceeded;
            warnings.AddRange(read.Warnings);

            if (read.Plan is null)
            {
                lastFailure = read.Failure!;
            }
            else if (!IsValid(read.Plan, request.TravelRequest.DurationDays, out string validationIssue))
            {
                lastFailure = CreateValidationFailure(validationIssue);
            }
            else
            {
                regenerationSucceeded = attempt > 1;
                return AgentResult<TripPlan>.Success(read.Plan, CreateMetadata(
                    attempt > 1 ? AgentExecutionStatus.SucceededAfterRegeneration : read.Status,
                    attempt,
                    structuredSucceeded,
                    rawRecoveryAttempted,
                    rawRecoverySucceeded,
                    regenerationAttempted,
                    regenerationSucceeded,
                    startedAt,
                    warnings));
            }

            if (attempt == MaximumAttempts || _failurePolicy.Decide(lastFailure) != AgentFailureAction.Regenerate)
            {
                break;
            }
        }

        AgentFailure failure = lastFailure is null
            ? CreateFailure(AgentFailureKind.Infrastructure, "agent-no-result", "The travel agent did not produce a result.", false)
            : lastFailure.Kind is AgentFailureKind.Validation or AgentFailureKind.StructuredOutput
                ? lastFailure with { Kind = AgentFailureKind.RegenerationFailed, Code = "agent-regeneration-failed", Retryable = false }
                : lastFailure;

        return AgentResult<TripPlan>.Failed(failure, CreateMetadata(
            failure.Kind == AgentFailureKind.RegenerationFailed ? AgentExecutionStatus.FailedRegeneration : ToStatus(failure.Kind),
            MaximumAttempts,
            structuredSucceeded,
            rawRecoveryAttempted,
            rawRecoverySucceeded,
            regenerationAttempted,
            false,
            startedAt,
            warnings));
    }

    public async Task<AgentSession> CreateSessionAsync(CancellationToken cancellationToken = default) =>
        await _agent.CreateSessionAsync(cancellationToken: cancellationToken);

    public JsonElement SerializeSession(AgentSession session) =>
        _agent.SerializeSessionAsync(session).AsTask().GetAwaiter().GetResult();

    public AgentSession DeserializeSession(JsonElement session) =>
        _agent.DeserializeSessionAsync(session).AsTask().GetAwaiter().GetResult();

    public async Task<TripPlanResponse> SendMessageAsync(
        string message,
        AgentSession session,
        CancellationToken cancellationToken = default,
        TravelInvocationContext? invocationContext = null)
    {
        _logger.LogInformation("Travel agent conversation turn starting");
        using ExecutionTraceScope trace = _traceRecorder.BeginRequest();
        using IDisposable invocation = _contextAccessor.Push(invocationContext ?? new TravelInvocationContext());
        AgentResponse<TripPlan> result = await _agent.RunAsync<TripPlan>(
            message,
            session,
            options: CreateStructuredOutputOptions(),
            cancellationToken: cancellationToken);
        StructuredReadResult read = ReadStructuredResult(() => result.Result, result.Text, _logger);
        if (read.Plan is null || !IsValid(read.Plan, null, out _))
        {
            throw new AgentExecutionException(read.Failure ?? CreateValidationFailure("Conversation response failed validation."),
                CreateMetadata(read.Status, 1, read.StructuredDeserializationSucceeded, read.RawRecoveryAttempted,
                    read.RawRecoverySucceeded, false, false, _timeProvider.GetUtcNow(), read.Warnings));
        }

        return new TripPlanResponse(read.Plan, trace.Complete());
    }

    private static ChatClientAgentRunOptions CreateStructuredOutputOptions() => new()
    {
        ChatOptions = new ChatOptions
        {
            ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema<TripPlan>()
        }
    };

    private static string CreatePrompt(TravelAgentRequest request) => $"""
        Create a travel plan for the following request:
        Destination: {request.TravelRequest.Destination}
        Duration in days: {request.TravelRequest.DurationDays}
        Traveller preferences: {request.TravelRequest.Preferences ?? "No additional preferences supplied."}
        Runtime context: {request.RuntimeContext ?? "None."}
        Traveller context: {request.TravelerContext ?? "None."}

        The workflow executed the validated tool plan in order.
        Execution results: {JsonSerializer.Serialize(request.ToolResults)}
        Use every successful result. Explicitly acknowledge failures without inventing data.
        Do not invoke tools; tool execution is complete.
        """;

    private static string CreateRegenerationPrompt(TravelAgentRequest request, AgentFailure? failure) => $"""
        Regenerate the complete TripPlan for this request using only the provided request and tool results.
        Correct this safe validation issue without adding unsupported itinerary content:
        Code: {failure?.Code ?? "unknown"}
        Path: {failure?.Path ?? "n/a"}
        Message: {failure?.Message ?? "The previous response was invalid."}

        {CreatePrompt(request)}
        """;

    internal static StructuredReadResult ReadStructuredResult(
        Func<TripPlan> resultAccessor,
        string rawResponse,
        ILogger logger)
    {
        try
        {
            return StructuredReadResult.Success(resultAccessor(), AgentExecutionStatus.Succeeded, true, false, false, []);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Travel agent structured response could not be deserialized. Attempting raw response parsing.");
            if (TryReadRawTripPlan(rawResponse, out TripPlan? recoveredPlan))
            {
                return StructuredReadResult.Success(recoveredPlan, AgentExecutionStatus.SucceededAfterRawRecovery, false, true, true,
                    ["Structured result deserialization failed; raw JSON was parsed without fabricating itinerary content."]);
            }

            return StructuredReadResult.Failed(CreateFailure(
                AgentFailureKind.StructuredOutput,
                "structured-output-invalid",
                "The travel agent returned structured output that did not match the TripPlan schema.",
                true,
                exception.Path), false, true, false, ["Raw JSON recovery failed."]);
        }
    }

    private static bool TryReadRawTripPlan(string rawResponse, out TripPlan? plan)
    {
        plan = null;
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return false;
        }

        try
        {
            plan = JsonSerializer.Deserialize<TripPlan>(ExtractJsonObject(rawResponse));
            return plan is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ExtractJsonObject(string rawResponse)
    {
        int start = rawResponse.IndexOf('{');
        int end = rawResponse.LastIndexOf('}');
        return start >= 0 && end > start ? rawResponse[start..(end + 1)] : rawResponse;
    }

    internal static bool IsValid(TripPlan? plan, int? requestedDuration, out string validationIssue)
    {
        validationIssue = plan switch
        {
            null => "The response was null.",
            _ when string.IsNullOrWhiteSpace(plan.Destination) => "Destination was empty.",
            { DurationDays: < 1 or > TravelPlanRequest.MaximumDurationDays } => "DurationDays was outside the supported range.",
            { Days: null } => "Days was null.",
            _ when requestedDuration.HasValue && plan.DurationDays != requestedDuration => "DurationDays did not match the request.",
            _ when plan.Days.Count != plan.DurationDays => "The day count did not match DurationDays.",
            _ when plan.Days.Select((day, index) => day is null || day.DayNumber != index + 1).Any(invalid => invalid)
                => "Day numbers were not sequential.",
            _ when plan.Days.Any(day => day.Activities is null) => "An activities collection was null.",
            _ when plan.Days.Any(day => day.Activities.Count == 0 || day.Activities.Any(activity => activity is null))
                => "A day contained no valid activities.",
            _ => string.Empty
        };

        return validationIssue.Length == 0;
    }

    private AgentExecutionMetadata CreateMetadata(AgentExecutionStatus status, int attemptCount,
        bool structuredDeserializationSucceeded, bool rawRecoveryAttempted, bool rawRecoverySucceeded,
        bool regenerationAttempted, bool regenerationSucceeded, DateTimeOffset startedAt, IReadOnlyList<string> warnings)
    {
        DateTimeOffset completedAt = _timeProvider.GetUtcNow();
        return new(nameof(TravelAgent), nameof(TripPlan), status, attemptCount, structuredDeserializationSucceeded,
            rawRecoveryAttempted, rawRecoverySucceeded, regenerationAttempted, regenerationSucceeded, startedAt,
            completedAt, Math.Max(0, (long)(completedAt - startedAt).TotalMilliseconds), warnings);
    }

    private static AgentFailure CreateValidationFailure(string validationIssue) => CreateFailure(
        AgentFailureKind.Validation,
        "trip-plan-validation-failed",
        "The travel agent returned a TripPlan that failed deterministic validation.",
        true,
        validationErrors: [new("$", "trip-plan-invalid", validationIssue)]);

    private static AgentFailure CreateFailure(AgentFailureKind kind, string code, string message, bool retryable,
        string? path = null, IReadOnlyList<AgentValidationError>? validationErrors = null, string? providerCode = null) =>
        new(kind, code, message, retryable, path, validationErrors, providerCode);

    private static AgentExecutionStatus ToStatus(AgentFailureKind kind) => kind switch
    {
        AgentFailureKind.StructuredOutput => AgentExecutionStatus.FailedStructuredOutput,
        AgentFailureKind.Validation => AgentExecutionStatus.FailedValidation,
        AgentFailureKind.Refusal => AgentExecutionStatus.Refused,
        AgentFailureKind.Timeout => AgentExecutionStatus.TimedOut,
        AgentFailureKind.RateLimit => AgentExecutionStatus.RateLimited,
        AgentFailureKind.Dependency => AgentExecutionStatus.FailedDependency,
        _ => AgentExecutionStatus.FailedInfrastructure
    };
}

public sealed record StructuredReadResult(
    TripPlan? Plan,
    AgentFailure? Failure,
    AgentExecutionStatus Status,
    bool StructuredDeserializationSucceeded,
    bool RawRecoveryAttempted,
    bool RawRecoverySucceeded,
    IReadOnlyList<string> Warnings)
{
    public static StructuredReadResult Success(TripPlan plan, AgentExecutionStatus status, bool structuredDeserializationSucceeded,
        bool rawRecoveryAttempted, bool rawRecoverySucceeded, IReadOnlyList<string> warnings) =>
        new(plan, null, status, structuredDeserializationSucceeded, rawRecoveryAttempted, rawRecoverySucceeded, warnings);

    public static StructuredReadResult Failed(AgentFailure failure, bool structuredDeserializationSucceeded,
        bool rawRecoveryAttempted, bool rawRecoverySucceeded, IReadOnlyList<string> warnings) =>
        new(null, failure, AgentExecutionStatus.FailedStructuredOutput, structuredDeserializationSucceeded,
            rawRecoveryAttempted, rawRecoverySucceeded, warnings);
}
