# Chapter 1 — Build Your First Agent

Build a small Microsoft Agent Framework (MAF) application backed by Azure OpenAI. The chapter starts with the application and model configuration, creates an `AIAgent`, invokes it through an HTTP endpoint, and then improves its behavior with explicit instructions.

## What You Will Learn

1. Create and configure the ASP.NET Core application.
2. Configure an Azure OpenAI endpoint, API key, and deployment.
3. Turn an Azure OpenAI chat client into the first `AIAgent`.
4. Invoke the agent and return its response.
5. See why an agent needs instructions.
6. Define its role, responsibilities, behavioral boundaries, and what it should and should not do.
7. Test the improved instruction contract.

The sample deliberately uses one standalone interaction per request. Conversations, memory, tools, workflows, persistence, and UI are introduced later.

## 1. Configure Azure OpenAI

Prerequisites are the .NET 9 SDK, an Azure OpenAI resource, and a chat-model deployment supported by MAF. Never store real credentials in `appsettings.json`.

From `chapter-01`, configure user secrets:

```bash
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://YOUR-RESOURCE.openai.azure.com/" --project src/SmartTravelPlanner.Api
dotnet user-secrets set "AzureOpenAI:ApiKey" "YOUR-API-KEY" --project src/SmartTravelPlanner.Api
dotnet user-secrets set "AzureOpenAI:DeploymentName" "YOUR-CHAT-DEPLOYMENT" --project src/SmartTravelPlanner.Api
```

The application binds these values to `AzureOpenAIOptions` and validates them at startup.

## 2. Create the Agent

`TravelAgent` creates an Azure OpenAI chat client and converts it to an `AIAgent`:

```csharp
_agent = new AzureOpenAIClient(new Uri(settings.Endpoint), new AzureKeyCredential(settings.ApiKey))
    .GetChatClient(settings.DeploymentName)
    .AsAIAgent(name: nameof(TravelAgent), instructions: TravelAgentInstructions.SystemPrompt);
```

The application registers `TravelAgent` behind `ITravelAgent`, keeping the endpoint small and easy to test.

## 3. Invoke the Agent

`CreateItineraryAsync` sends the user's prompt to `AIAgent.RunAsync`. The returned `AgentResponse.Text` becomes the API response from `POST /api/travel/plan`.

```csharp
AgentResponse result = await _agent.RunAsync(prompt, cancellationToken: cancellationToken);
return result.Text;
```

The flow is: client → controller → `TravelAgent` → Azure OpenAI → response.

## 4. Improve the Instructions

A vague instruction such as “help plan a trip” leaves the response format and safety boundaries unclear. `TravelAgentInstructions.SystemPrompt` makes the intended behavior explicit:

- **Role:** professional travel-planning assistant.
- **Responsibilities:** identify the destination and duration, build a practical day-by-day plan, respect preferences, state assumptions, and include practical tips.
- **Behavioral boundaries:** do not book travel, invent verification, present uncertainty as fact, or recommend unsafe or illegal activities.
- **Output contract:** return concise Markdown with an overview, one section per day, and practical tips.

The instruction test checks these observable requirements. This does not evaluate model quality; it protects the prompt contract so a later edit cannot silently remove the role, responsibilities, or boundaries. To observe the behavioral difference with Azure OpenAI, try the same underspecified or live-data request before and after passing `TravelAgentInstructions.SystemPrompt`, for example:

```text
Plan two days in Dubai and confirm today's ticket prices and availability.
```

With the improved instructions, the response should still provide a two-day plan but should not claim that live prices or availability were verified.

## Run and Test

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/SmartTravelPlanner.Api
```

Then send a request to the address printed by the application:

```bash
curl -X POST "https://localhost:<port>/api/travel/plan" \
  -H "Content-Type: application/json" \
  -d '{"prompt":"Plan a two-day trip to Dubai."}'
```

In Development, `/swagger` provides an interactive alternative. The endpoint rejects empty prompts, while global error handling returns safe Problem Details responses.

## Project Layout

```text
chapter-01/
├── src/SmartTravelPlanner.Api/        # API, Azure configuration, and AIAgent
├── tests/SmartTravelPlanner.Api.Tests/ # Endpoint and instruction-contract tests
├── Directory.Packages.props
└── SmartTravelPlanner.sln
```
