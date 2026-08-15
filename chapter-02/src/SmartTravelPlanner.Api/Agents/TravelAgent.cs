using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using SmartTravelPlanner.Api.Configuration;
using SmartTravelPlanner.Api.Contracts;
using SmartTravelPlanner.Api.Models.TravelPlanning;

namespace SmartTravelPlanner.Api.Agents;

public sealed class TravelAgent : ITravelAgent
{
    private readonly AIAgent _agent;
    private readonly ILogger<TravelAgent> _logger;

    public TravelAgent(IOptions<AzureOpenAIOptions> options, ILogger<TravelAgent> logger)
    {
        AzureOpenAIOptions settings = options.Value;
        _logger = logger;
        _agent = new AzureOpenAIClient(new Uri(settings.Endpoint), new AzureKeyCredential(settings.ApiKey))
            .GetChatClient(settings.DeploymentName)
            .AsAIAgent(name: nameof(TravelAgent), instructions: TravelAgentInstructions.SystemPrompt);
    }

    public async Task<TripPlan> CreateItineraryAsync(
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

        AgentResponse<TripPlan> result = await _agent.RunAsync<TripPlan>(
            prompt,
            cancellationToken: cancellationToken);
        TripPlan plan = result.Result;

        if (!IsValid(plan, request.DurationDays, out string validationIssue))
        {
            _logger.LogError("Travel agent returned an invalid structured response: {ValidationIssue}", validationIssue);
            throw new InvalidOperationException("The travel agent returned an invalid travel plan.");
        }

        _logger.LogInformation("Travel agent execution completed with {DayCount} days", plan.Days.Count);
        return plan;
    }

    private static bool IsValid(TripPlan? plan, int requestedDuration, out string validationIssue)
    {
        validationIssue = plan switch
        {
            null => "The response was null.",
            _ when string.IsNullOrWhiteSpace(plan.Destination) => "Destination was empty.",
            { DurationDays: < 1 or > TravelPlanRequest.MaximumDurationDays } => "DurationDays was outside the supported range.",
            { Days: null } => "Days was null.",
            _ when plan.DurationDays != requestedDuration => "DurationDays did not match the request.",
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
