using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartTravelPlanner.Api.Agents;
using SmartTravelPlanner.Api.Contracts;
using SmartTravelPlanner.Api.Models.TravelPlanning;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class TravelPlanEndpointTests
{
    [Theory]
    [InlineData("", 2)]
    [InlineData("Jaipur", 0)]
    [InlineData("Jaipur", 15)]
    public async Task InvalidRequestReturnsBadRequest(string destination, int durationDays)
    {
        using TestApplicationFactory factory = new(CreatePlan());
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/travel/plan",
            new TravelPlanRequest(destination, durationDays));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Agent.CallCount);
    }

    [Fact]
    public async Task PreferencesOverMaximumLengthReturnsBadRequest()
    {
        using TestApplicationFactory factory = new(CreatePlan());
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/travel/plan",
            new TravelPlanRequest("Jaipur", 2, new string('a', 501)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Agent.CallCount);
    }

    [Fact]
    public async Task ValidRequestReturnsStructuredTripPlan()
    {
        TripPlan itinerary = CreatePlan();
        using TestApplicationFactory factory = new(itinerary);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/travel/plan",
            new TravelPlanRequest("Jaipur", 2, "Local food"));
        TripPlan? result = await response.Content.ReadFromJsonAsync<TripPlan>();
        string json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal("Jaipur", result.Destination);
        Assert.Equal(2, result.DurationDays);
        Assert.Equal(2, result.Days.Count);
        Assert.Equal(new[] { 1, 2 }, result.Days.Select(day => day.DayNumber));
        Assert.All(result.Days, day => Assert.NotEmpty(day.Activities));
        Assert.All(result.Days, day => Assert.NotNull(day.Activities));
        Assert.StartsWith("{", json.TrimStart());
        Assert.DoesNotContain("\"response\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Jaipur", factory.Agent.LastRequest?.Destination);
        Assert.Equal(1, factory.Agent.CallCount);
    }

    [Fact]
    public async Task AgentFailureReturnsControlledServerError()
    {
        using TestApplicationFactory factory = new(CreatePlan(), new InvalidOperationException("provider details"));
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/travel/plan",
            new TravelPlanRequest("Jaipur", 2));
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Unable to create a travel plan", body);
        Assert.DoesNotContain("provider details", body);
    }

    private static TripPlan CreatePlan() => new()
    {
        Destination = "Jaipur",
        DurationDays = 2,
        Summary = "A balanced introduction to Jaipur.",
        Days =
        [
            CreateDay(1, "Historic Jaipur"),
            CreateDay(2, "Culture and markets")
        ]
    };

    private static TripDay CreateDay(int number, string title) => new()
    {
        DayNumber = number,
        Title = title,
        Activities =
        [
            new TripActivity
            {
                Time = "09:00",
                Name = "Guided walk",
                Description = "Explore local landmarks.",
                Category = "Sightseeing",
                Notes = "Arrive early."
            }
        ]
    };

    private sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        public TestApplicationFactory(TripPlan response, Exception? exception = null) =>
            Agent = new FakeTravelAgent(response, exception);

        public FakeTravelAgent Agent { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AzureOpenAI:Endpoint"] = "https://example.openai.azure.com/",
                    ["AzureOpenAI:ApiKey"] = "test-key",
                    ["AzureOpenAI:DeploymentName"] = "test-deployment"
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITravelAgent>();
                services.AddSingleton<ITravelAgent>(Agent);
            });
        }
    }

    public sealed class FakeTravelAgent(TripPlan response, Exception? exception) : ITravelAgent
    {
        public int CallCount { get; private set; }
        public TravelPlanRequest? LastRequest { get; private set; }

        public Task<TripPlan> CreateItineraryAsync(
            TravelPlanRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return exception is null
                ? Task.FromResult(response)
                : Task.FromException<TripPlan>(exception);
        }
    }
}
