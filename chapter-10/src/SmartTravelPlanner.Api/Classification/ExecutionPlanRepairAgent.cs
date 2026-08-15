using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using SmartTravelPlanner.Api.Configuration;

namespace SmartTravelPlanner.Api.Classification;

public sealed class ExecutionPlanRepairAgent : IExecutionPlanRepairAgent
{
    private readonly AIAgent _agent;

    public ExecutionPlanRepairAgent(IOptions<AzureOpenAIOptions> options)
    {
        AzureOpenAIOptions settings = options.Value;
        _agent = new AzureOpenAIClient(new Uri(settings.Endpoint), new AzureKeyCredential(settings.ApiKey))
            .GetChatClient(settings.DeploymentName)
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = nameof(ExecutionPlanRepairAgent),
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        Repair an invalid ExecutionPlan using the original request and validation errors.
                        Return only the corrected ExecutionPlan. Preserve requested tools and their order.
                        Use canonical argument keys: destination, origin, amount, from, to, and city.
                        Do not execute tools, answer the user, add unrequested tools, or invent missing values.
                        """
                }
            });
    }

    public async Task<ExecutionPlan> RepairAsync(string originalRequest, ExecutionPlan invalidPlan,
        IReadOnlyList<string> validationErrors, CancellationToken cancellationToken = default)
    {
        string prompt = $"""
            Original request: {originalRequest}
            Invalid execution plan: {JsonSerializer.Serialize(invalidPlan)}
            Validation errors: {JsonSerializer.Serialize(validationErrors)}
            """;
        AgentResponse<ExecutionPlan> response = await _agent.RunAsync<ExecutionPlan>(prompt,
            options: new ChatClientAgentRunOptions
            {
                ChatOptions = new ChatOptions { ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema<ExecutionPlan>() }
            }, cancellationToken: cancellationToken);
        return response.Result;
    }
}
