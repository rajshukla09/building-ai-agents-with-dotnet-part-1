using SmartTravelPlanner.Api.Agents;
using Xunit;

namespace SmartTravelPlanner.Api.Tests.Agents;

public sealed class TravelAgentInstructionsTests
{
    [Fact]
    public void SystemPromptDefinesRoleResponsibilitiesAndBoundaries()
    {
        string instructions = TravelAgentInstructions.SystemPrompt;

        Assert.Contains("Your role", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Responsibilities", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Behavioral boundaries", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not perform bookings", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one day section for every requested travel day", instructions, StringComparison.OrdinalIgnoreCase);
    }
}
