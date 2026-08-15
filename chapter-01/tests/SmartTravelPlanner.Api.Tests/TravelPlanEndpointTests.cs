using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartTravelPlanner.Api.Agents;
using SmartTravelPlanner.Api.Contracts;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class TravelPlanEndpointTests
{
    [Fact]
    public async Task EmptyPromptReturnsBadRequest()
    {
        using TestApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/travel/plan",
            new TravelPlanRequest("   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Prompt is required", body);
        Assert.Equal(0, factory.Agent.CallCount);
    }

    [Fact]
    public async Task ValidPromptInvokesAgentAndMapsItsResponse()
    {
        const string itinerary = "Day 1: Explore the old town.";
        using TestApplicationFactory factory = new(itinerary);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/travel/plan",
            new TravelPlanRequest("Plan a weekend away."));
        TravelPlanResponse? result = await response.Content.ReadFromJsonAsync<TravelPlanResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(itinerary, result?.Response);
        Assert.Equal("Plan a weekend away.", factory.Agent.LastPrompt);
        Assert.Equal(1, factory.Agent.CallCount);
    }

    [Fact]
    public async Task AgentFailureReturnsControlledServerError()
    {
        using TestApplicationFactory factory = new(exception: new InvalidOperationException("provider details"));
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/travel/plan",
            new TravelPlanRequest("Plan a trip."));
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Unable to create a travel plan", body);
        Assert.DoesNotContain("provider details", body);
    }

    private sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        public TestApplicationFactory(string response = "Test itinerary", Exception? exception = null)
        {
            Agent = new FakeTravelAgent(response, exception);
        }

        public FakeTravelAgent Agent { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AzureOpenAI:Endpoint"] = "https://example.openai.azure.com/",
                    ["AzureOpenAI:ApiKey"] = "test-key",
                    ["AzureOpenAI:DeploymentName"] = "test-deployment"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITravelAgent>();
                services.AddSingleton<ITravelAgent>(Agent);
            });
        }
    }

    public sealed class FakeTravelAgent(string response, Exception? exception) : ITravelAgent
    {
        public int CallCount { get; private set; }

        public string? LastPrompt { get; private set; }

        public Task<string> CreateItineraryAsync(string prompt, CancellationToken cancellationToken)
        {
            CallCount++;
            LastPrompt = prompt;

            return exception is null
                ? Task.FromResult(response)
                : Task.FromException<string>(exception);
        }
    }
}
