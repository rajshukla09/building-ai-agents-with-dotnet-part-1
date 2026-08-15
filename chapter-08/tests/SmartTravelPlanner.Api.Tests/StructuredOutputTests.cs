using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartTravelPlanner.Api.Agents;
using SmartTravelPlanner.Api.Models.TravelPlanning;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class StructuredOutputTests
{
    [Fact]
    public void ValidStructuredResponseDeserializes()
    {
        TripPlan? plan = JsonSerializer.Deserialize<TripPlan>(ValidJson);
        Assert.NotNull(plan);
        Assert.True(TravelAgent.IsValid(plan, 1, out string issue), issue);
    }

    [Fact]
    public void MalformedDaysItemFailsDeserialization() =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TripPlan>(
            """{"Destination":"Tokyo","DurationDays":1,"Summary":"Trip","Days":[17]}"""));

    [Fact]
    public void MissingRequiredPropertiesFailsDeserialization() =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TripPlan>(
            """{"Destination":"Tokyo","DurationDays":1,"Days":[]}"""));

    [Fact]
    public void DeserializationFailureIsHandledAndRawResponseIsLogged()
    {
        RecordingLogger logger = new();
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            TravelAgent.ReadStructuredResult(
                () => throw new JsonException("bad JSON"),
                "{malformed-response",
                logger));

        Assert.IsType<JsonException>(exception.InnerException);
        Assert.Contains("could not be read as a travel plan", exception.Message);
        Assert.Contains("{malformed-response", logger.Message);
    }

    [Fact]
    public void BusinessValidationStillRejectsIncompletePlans()
    {
        TripPlan invalid = JsonSerializer.Deserialize<TripPlan>(
            """{"Destination":"Tokyo","DurationDays":2,"Summary":"Trip","Days":[]}""")!;
        Assert.False(TravelAgent.IsValid(invalid, 2, out string issue));
        Assert.Equal("The day count did not match DurationDays.", issue);
    }

    private const string ValidJson = """
        {"Destination":"Tokyo","DurationDays":1,"Summary":"Trip","Days":[{"DayNumber":1,"Title":"Arrival","Activities":[{"Time":"09:00","Name":"Garden","Description":"Visit","Category":"Sightseeing","Notes":""}]}]}
        """;

    private sealed class RecordingLogger : ILogger
    {
        public string Message { get; private set; } = string.Empty;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Message = formatter(state, exception);
    }
}
