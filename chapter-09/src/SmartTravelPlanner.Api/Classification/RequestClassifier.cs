using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using SmartTravelPlanner.Api.Configuration;

namespace SmartTravelPlanner.Api.Classification;

public sealed class RequestClassifier : IRequestClassifier
{
    private const string Instructions = """
        Classify the user's travel request. Return only an ExecutionPlan structured result.
        Choose the primary intent:
        - TravelPlanning for itinerary creation or revision.
        - DistanceLookup for a focused distance or how-far question.
        - CurrencyConversion for a focused conversion.
        - LocalTime for a focused local-time question.
        - WeatherLookup for a focused weather question.
        - Unknown when none applies.
        Add zero, one, or multiple required tool steps. Preserve the order in which the user asks
        for Weather, Distance, Currency, and LocalTime operations. Orders must start at 1 and be
        consecutive. Use argument keys destination; origin and destination; amount, from, and to;
        or city respectively. A normal travel plan need not add a weather step unless requested.
        Do not answer the request, call tools, or invent missing parameters. Currency codes must
        be uppercase ISO-style three-letter codes.
        """;

    private readonly AIAgent _classifier;
    private readonly ILogger<RequestClassifier> _logger;

    public RequestClassifier(
        IOptions<AzureOpenAIOptions> options,
        ILogger<RequestClassifier> logger)
    {
        AzureOpenAIOptions settings = options.Value;
        _logger = logger;
        _classifier = new AzureOpenAIClient(new Uri(settings.Endpoint), new AzureKeyCredential(settings.ApiKey))
            .GetChatClient(settings.DeploymentName)
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = nameof(RequestClassifier),
                ChatOptions = new ChatOptions { Instructions = Instructions }
            });
    }

    public async Task<ExecutionPlan> ClassifyAsync(
        string request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        try
        {
            AgentResponse<ExecutionPlan> response = await _classifier.RunAsync<ExecutionPlan>(
                request,
                options: new ChatClientAgentRunOptions
                {
                    ChatOptions = new ChatOptions
                    {
                        ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema<ExecutionPlan>()
                    }
                },
                cancellationToken: cancellationToken);
            ExecutionPlan plan = response.Result;
            _logger.LogInformation("Request classified as {Intent} with {StepCount} execution steps",
                plan.Intent, plan.Steps.Count);
            return plan;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Request classification failed");
            throw;
        }
    }
}
