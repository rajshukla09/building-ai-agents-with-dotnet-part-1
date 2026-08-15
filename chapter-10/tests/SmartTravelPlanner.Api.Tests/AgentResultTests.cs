using SmartTravelPlanner.Api.Agents.Results;
using SmartTravelPlanner.Api.Models.TravelPlanning;
using Xunit;

namespace SmartTravelPlanner.Api.Tests;

public sealed class AgentResultTests
{
    [Fact]
    public void SuccessRequiresValueAndCarriesMetadata()
    {
        AgentExecutionMetadata metadata = Metadata(AgentExecutionStatus.Succeeded);
        AgentResult<TripPlan> result = AgentResult<TripPlan>.Success(CreatePlan(), metadata);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Null(result.Failure);
        Assert.Equal(metadata, result.Metadata);
    }

    [Fact]
    public void FailedRequiresFailureAndCarriesMetadata()
    {
        AgentExecutionMetadata metadata = Metadata(AgentExecutionStatus.FailedStructuredOutput);
        AgentFailure failure =
            new(AgentFailureKind.StructuredOutput, "structured-output-invalid", "Bad JSON", true, "$.days[1]");
        AgentResult<TripPlan> result = AgentResult<TripPlan>.Failed(failure, metadata);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(failure, result.Failure);
        Assert.Equal("$.days[1]", result.Failure.Path);
    }

    [Theory]
    [InlineData(AgentFailureKind.StructuredOutput, AgentFailureAction.Regenerate)]
    [InlineData(AgentFailureKind.Validation, AgentFailureAction.Regenerate)]
    [InlineData(AgentFailureKind.RateLimit, AgentFailureAction.Retry)]
    [InlineData(AgentFailureKind.Timeout, AgentFailureAction.Retry)]
    [InlineData(AgentFailureKind.Refusal, AgentFailureAction.Stop)]
    public void FailurePolicyMapsExpectedActions(AgentFailureKind kind, AgentFailureAction expected)
    {
        DefaultAgentFailurePolicy policy = new();
        Assert.Equal(expected, policy.Decide(new AgentFailure(kind, kind.ToString(), "Failure", Retryable: false)));
    }

    private static AgentExecutionMetadata Metadata(AgentExecutionStatus status)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new("TravelAgent", nameof(TripPlan), status, 1, true, false, false, false, false, now, now, 0, []);
    }

    private static TripPlan CreatePlan() => new() {
        Destination = "Tokyo", DurationDays = 1, Summary = "Trip",
        Days = [new TripDay { DayNumber = 1, Title = "Arrival",
                              Activities = [new TripActivity { Time = "09:00", Name = "Garden", Description = "Visit",
                                                               Category = "Sightseeing", Notes = "" }] }]
    };
}
