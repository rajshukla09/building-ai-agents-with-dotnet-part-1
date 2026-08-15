using System.Text.Json;
using System.Text.Json.Serialization;
using SmartTravelPlanner.Contracts;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class RequestIntentSerializationTests
{
    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    [Theory]
    [InlineData(RequestIntent.TravelPlanning)]
    [InlineData(RequestIntent.DistanceLookup)]
    [InlineData(RequestIntent.CurrencyConversion)]
    [InlineData(RequestIntent.LocalTime)]
    [InlineData(RequestIntent.WeatherLookup)]
    [InlineData(RequestIntent.Unknown)]
    public void RequestIntent_SerializesAsString_AndRoundTrips(RequestIntent intent)
    {
        var plan = new ExecutionPlanDto(intent, []);
        string json = JsonSerializer.Serialize(plan, Json);

        Assert.Contains($"\"intent\":\"{intent}\"", json);
        ExecutionPlanDto? deserialized = JsonSerializer.Deserialize<ExecutionPlanDto>(json, Json);
        Assert.Equal(intent, deserialized?.Intent);
    }

    [Fact]
    public void NumericIntent_IsStillReadable_ForCompatibility()
    {
        ExecutionPlanDto? plan = JsonSerializer.Deserialize<ExecutionPlanDto>("{\"intent\":0,\"steps\":[]}", Json);
        Assert.Equal(RequestIntent.TravelPlanning, plan?.Intent);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
