using Azure;
using Azure.AI.OpenAI;
using System.Text.Json;
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

namespace SmartTravelPlanner.Api.Agents;

public sealed class TravelAgent : ITravelAgent, IAgentSessionSerializer
{
    private readonly AIAgent _agent;
    private readonly ILogger<TravelAgent> _logger;
    private readonly IExecutionTraceRecorder _traceRecorder;

    public TravelAgent(
        IOptions<AzureOpenAIOptions> options,
        ILogger<TravelAgent> logger,
        WeatherTool weatherTool,
        CurrencyTool currencyTool,
        TimeZoneTool timeZoneTool,
        DistanceTool distanceTool,
        IExecutionTraceRecorder traceRecorder)
    {
        AzureOpenAIOptions settings = options.Value;
        _logger = logger;
        _traceRecorder = traceRecorder;
        _agent = new AzureOpenAIClient(new Uri(settings.Endpoint), new AzureKeyCredential(settings.ApiKey))
            .GetChatClient(settings.DeploymentName)
            .AsAIAgent(
                name: nameof(TravelAgent),
                instructions: TravelAgentInstructions.SystemPrompt,
                tools:
                [
                    AIFunctionFactory.Create(weatherTool.GetWeather),
                    AIFunctionFactory.Create(currencyTool.ConvertCurrency),
                    AIFunctionFactory.Create(timeZoneTool.GetLocalTime),
                    AIFunctionFactory.Create(distanceTool.GetDistance)
                ]);
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
        AgentResponse<TripPlan> result = await _agent.RunAsync<TripPlan>(prompt, cancellationToken: cancellationToken);
        TripPlan plan = Validate(result.Result, request.DurationDays);
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
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Travel agent conversation turn starting");
        using ExecutionTraceScope trace = _traceRecorder.BeginRequest();
        AgentResponse<TripPlan> result = await _agent.RunAsync<TripPlan>(message, session, cancellationToken: cancellationToken);
        TripPlan plan = Validate(result.Result);
        return new TripPlanResponse(plan, trace.Complete());
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

    private static bool IsValid(TripPlan? plan, int? requestedDuration, out string validationIssue)
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
