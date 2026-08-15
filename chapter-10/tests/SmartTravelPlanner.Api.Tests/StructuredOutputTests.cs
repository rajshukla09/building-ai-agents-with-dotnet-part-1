using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartTravelPlanner.Api.Agents;
using SmartTravelPlanner.Api.Agents.Results;
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
    public void NonObjectDayItemsReturnStructuredOutputFailure()
    {
        RecordingLogger logger = new();
        StructuredReadResult result = TravelAgent.ReadStructuredResult(
            () => throw new JsonException("The JSON value could not be converted to TripDay.", "$.days[1]", 0, 3174),
            """
            {"Destination":"Tokyo","DurationDays":2,"Summary":"Trip","Days":[{"DayNumber":1,"Title":"Arrival","Activities":[{"Time":"09:00","Name":"Garden","Description":"Visit","Category":"Sightseeing","Notes":""}]},"Visit museums and have dinner in Shinjuku."]}
            """,
            logger);

        Assert.Null(result.Plan);
        Assert.NotNull(result.Failure);
        Assert.Equal(AgentFailureKind.StructuredOutput, result.Failure.Kind);
        Assert.Equal("$.days[1]", result.Failure.Path);
        Assert.True(result.RawRecoveryAttempted);
        Assert.False(result.RawRecoverySucceeded);
    }

    [Fact]
    public void DeserializationFailureIsHandledAsFailedResultAndRawResponseIsNotLogged()
    {
        RecordingLogger logger = new();
        StructuredReadResult result = TravelAgent.ReadStructuredResult(
            () => throw new JsonException("bad JSON"),
            "{malformed-response",
            logger);

        Assert.Null(result.Plan);
        Assert.NotNull(result.Failure);
        Assert.Equal(AgentFailureKind.StructuredOutput, result.Failure.Kind);
        Assert.DoesNotContain("{malformed-response", logger.Message);
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
