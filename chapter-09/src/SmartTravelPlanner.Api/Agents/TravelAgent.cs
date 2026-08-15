using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using SmartTravelPlanner.Api.Configuration;
using SmartTravelPlanner.Api.Contracts;
using SmartTravelPlanner.Api.Models.TravelPlanning;
using SmartTravelPlanner.Api.Conversations;
using SmartTravelPlanner.Api.Tools;
using Microsoft.Extensions.AI;
using SmartTravelPlanner.Api.Execution;
using SmartTravelPlanner.Api.Models.Execution;
using SmartTravelPlanner.Api.Context;
using SmartTravelPlanner.Api.Classification;
using System.Text.Json;

namespace SmartTravelPlanner.Api.Agents;

public sealed class TravelAgent : ITravelAgent, IAgentSessionSerializer
{
    private readonly AIAgent _agent;
    private readonly ILogger<TravelAgent> _logger;
    private readonly IExecutionTraceRecorder _traceRecorder;
    private readonly TravelInvocationContextAccessor _contextAccessor;
    private readonly IExecutionPlanProvider _executionPlanProvider;
    private readonly IExecutionPlanExecutor _executionPlanExecutor;

    public TravelAgent(
        IOptions<AzureOpenAIOptions> options,
        ILogger<TravelAgent> logger,
        WeatherTool weatherTool, CurrencyTool currencyTool, TimeZoneTool timeZoneTool, DistanceTool distanceTool,
        IExecutionPlanProvider executionPlanProvider, IToolExecutionPipeline toolPipeline,
        IExecutionPlanExecutor executionPlanExecutor,
        IExecutionTraceRecorder traceRecorder,
        TravelInvocationContextAccessor contextAccessor,
        TravelerMemoryContextProvider memoryContextProvider,
        RuntimeTravelContextProvider runtimeContextProvider)
    {
        AzureOpenAIOptions settings = options.Value;
        _logger = logger;
        _traceRecorder = traceRecorder;
        _contextAccessor = contextAccessor;
        _executionPlanProvider = executionPlanProvider;
        _executionPlanExecutor = executionPlanExecutor;
        _agent = new AzureOpenAIClient(new Uri(settings.Endpoint), new AzureKeyCredential(settings.ApiKey))
            .GetChatClient(settings.DeploymentName)
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = nameof(TravelAgent),
                ChatOptions = new ChatOptions
                {
                    Instructions = TravelAgentInstructions.SystemPrompt,
                    Tools =
                    [
                        AIFunctionFactory.Create((string destination) => toolPipeline.ExecuteModelSelected(
                            nameof(WeatherTool), new { destination }, () => weatherTool.GetWeather(destination)), "GetWeather"),
                        AIFunctionFactory.Create((string from, string to, decimal amount) => toolPipeline.ExecuteModelSelected(
                            nameof(CurrencyTool), new { from, to, amount }, () => currencyTool.ConvertCurrency(from, to, amount)), "ConvertCurrency"),
                        AIFunctionFactory.Create((string city) => toolPipeline.ExecuteModelSelected(
                            nameof(TimeZoneTool), new { city }, () => timeZoneTool.GetLocalTime(city)), "GetLocalTime"),
                        AIFunctionFactory.Create((string origin, string destination) => toolPipeline.ExecuteModelSelected(
                            nameof(DistanceTool), new { origin, destination }, () => distanceTool.GetDistance(origin, destination)), "GetDistance")
                    ]
                },
                AIContextProviders = [memoryContextProvider, runtimeContextProvider]
            });
    }

    public async Task<TripPlanResponse> CreateItineraryAsync(
        TravelPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Travel agent execution starting for a {DurationDays}-day itinerary",
            request.DurationDays);

        string prompt = $"""
            Create a travel plan for the following request:
            Destination: {request.Destination}
            Duration in days: {request.DurationDays}
            Traveller preferences: {request.Preferences ?? "No additional preferences supplied."}
            """;

        using ExecutionTraceScope trace = _traceRecorder.BeginRequest();
        using IDisposable invocation = _contextAccessor.Push(new TravelInvocationContext(
            Destination: request.Destination, DurationDays: request.DurationDays));
        prompt = await ApplyRoutingAsync(prompt, cancellationToken);
        AgentResponse<TripPlan> result = await _agent.RunAsync<TripPlan>(
            prompt,
            options: CreateStructuredOutputOptions(),
            cancellationToken: cancellationToken);
        TripPlan plan = Validate(GetStructuredResult(result), request.DurationDays);
        return new TripPlanResponse(plan, trace.Complete());
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
        message = await ApplyRoutingAsync(message, cancellationToken);
        AgentResponse<TripPlan> result = await _agent.RunAsync<TripPlan>(
            message,
            session,
            options: CreateStructuredOutputOptions(),
            cancellationToken: cancellationToken);
        TripPlan plan = Validate(GetStructuredResult(result));
        return new TripPlanResponse(plan, trace.Complete());
    }

    private async Task<string> ApplyRoutingAsync(string request, CancellationToken cancellationToken)
    {
        ExecutionPlan plan = await _executionPlanProvider.CreateAsync(request, cancellationToken);
        return await _executionPlanExecutor.EnrichRequestAsync(request, plan, cancellationToken);
    }

    private TripPlan GetStructuredResult(AgentResponse<TripPlan> result)
        => ReadStructuredResult(() => result.Result, result.Text, _logger);

    private static ChatClientAgentRunOptions CreateStructuredOutputOptions() => new()
    {
        ChatOptions = new ChatOptions
        {
            ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema<TripPlan>()
        }
    };

    internal static TripPlan ReadStructuredResult(
        Func<TripPlan> resultAccessor,
        string rawResponse,
        ILogger logger)
    {
        try
        {
            return resultAccessor();
        }
        catch (JsonException exception)
        {
            logger.LogError(
                exception,
                "Travel agent structured response could not be deserialized. Raw response: {RawResponse}",
                rawResponse);
            throw new InvalidOperationException(
                "The travel agent returned a response that could not be read as a travel plan.",
                exception);
        }
    }

    private TripPlan Validate(TripPlan plan, int? requestedDuration = null)
    {
        if (!IsValid(plan, requestedDuration, out string validationIssue))
        {
            _logger.LogError("Travel agent returned an invalid structured response: {ValidationIssue}", validationIssue);
            throw new InvalidOperationException("The travel agent returned an invalid travel plan.");
        }

        _logger.LogInformation("Travel agent execution completed with {DayCount} days", plan.Days.Count);
        return plan;
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
}
