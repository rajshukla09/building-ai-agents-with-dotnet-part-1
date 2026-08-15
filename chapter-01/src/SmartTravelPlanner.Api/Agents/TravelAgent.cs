using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using SmartTravelPlanner.Api.Configuration;

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

    public async Task<string> CreateItineraryAsync(string prompt, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Travel agent execution starting");
        AgentResponse result = await _agent.RunAsync(prompt, cancellationToken: cancellationToken);
        string response = result.Text;
        _logger.LogInformation("Travel agent execution completed with {ResponseLength} characters", response.Length);
        return response;
    }
}
