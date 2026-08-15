using System.Text.Json;
using Microsoft.Agents.AI;

namespace SmartTravelPlanner.Api.Conversations;

/// <summary>Converts framework sessions to and from the framework-supported JSON representation.</summary>
public interface IAgentSessionSerializer
{
    JsonElement SerializeSession(AgentSession session);

    AgentSession DeserializeSession(JsonElement session);
}
