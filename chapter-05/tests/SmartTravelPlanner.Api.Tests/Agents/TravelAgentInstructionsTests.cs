using System.Reflection;
using SmartTravelPlanner.Api.Agents;
using Xunit;

namespace SmartTravelPlanner.Api.Tests.Agents;

public sealed class TravelAgentInstructionsTests
{
    [Fact]
    public void SystemPromptIsNotNullOrWhitespace()
    {
        Assert.False(string.IsNullOrWhiteSpace(TravelAgentInstructions.SystemPrompt));
    }

    [Theory]
    [InlineData("professional travel-planning assistant")]
    [InlineData("requested trip duration")]
    [InlineData("day-by-day planning")]
    [InlineData("Destination is the normalized destination")]
    [InlineData("Days contains exactly one TripDay")]
    [InlineData("DayNumber values are sequential")]
    [InlineData("logical daily and geographical sequence")]
    [InlineData("Activities are ordered chronologically")]
    [InlineData("Sightseeing, Food, Travel, Rest, Shopping")]
    [InlineData("real-time verification")]
    [InlineData("have been booked")]
    [InlineData("assumptions needed because information is missing")]
    [InlineData("earlier messages in the current conversation")]
    [InlineData("complete updated TripPlan")]
    public void SystemPromptContainsImportantRequirement(string requirement)
    {
        Assert.Contains(requirement, TravelAgentInstructions.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TravelAgentDoesNotContainAnEmbeddedInstructionString()
    {
        FieldInfo[] stringFields = typeof(TravelAgent)
            .GetFields(BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(field => field.FieldType == typeof(string))
            .ToArray();

        Assert.Empty(stringFields);
    }
}
