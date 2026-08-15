# Chapter 2 – Structured Outputs with Microsoft Agent Framework

## Objective

Chapter 2 starts with the complete Chapter 1 application and changes one concept: the travel agent now returns a strongly typed itinerary instead of prose. Azure OpenAI configuration, dependency injection, Swagger, validation, safe error handling, and the standalone request model remain intact. Sessions, memory, tools, workflows, and persistence are deliberately out of scope.

## Why structured output?

Plain text is readable, but callers cannot reliably locate a destination, count itinerary days, or render activities without parsing formatting that a model may vary. A native structured response gives the API a compile-time contract, produces a complete Swagger schema, and lets the application validate essential itinerary invariants before returning data.

The response hierarchy is:

```text
TripPlan (Destination, DurationDays, Summary, Days)
└── TripDay (DayNumber, Title, Activities)
    └── TripActivity (Time, Name, Description, Category, Notes)
```

`TravelAgent` calls Microsoft Agent Framework's generic `AIAgent.RunAsync<TripPlan>` API and reads `AgentResponse<TripPlan>.Result`. The framework requests and materializes the typed result; the application does not scrape Markdown, repair JSON, or deserialize raw model text. The agent then checks destination, duration, day count and numbering, and activity collections. An invalid model response is logged and becomes a controlled Problem Details response.

## Configure and run

Install the .NET 9 SDK, then configure the same Azure OpenAI values used in Chapter 1. From `chapter-02`:

```bash
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://YOUR-RESOURCE.openai.azure.com/" --project src/SmartTravelPlanner.Api
dotnet user-secrets set "AzureOpenAI:ApiKey" "YOUR-API-KEY" --project src/SmartTravelPlanner.Api
dotnet user-secrets set "AzureOpenAI:DeploymentName" "YOUR-CHAT-DEPLOYMENT" --project src/SmartTravelPlanner.Api
dotnet restore
dotnet build
dotnet test
dotnet run --project src/SmartTravelPlanner.Api
```

Credentials can instead use `AzureOpenAI__Endpoint`, `AzureOpenAI__ApiKey`, and `AzureOpenAI__DeploymentName` environment variables. Do not commit real credentials.

## Request and response

```bash
curl -X POST "https://localhost:<port>/api/travel/plan" \
  -H "Content-Type: application/json" \
  -d '{"destination":"Jaipur","durationDays":3,"preferences":"Historic sites and local food"}'
```

The response is a JSON object rather than a string wrapper:

```json
{
  "destination": "Jaipur",
  "durationDays": 3,
  "summary": "A balanced three-day introduction to Jaipur.",
  "days": [
    {
      "dayNumber": 1,
      "title": "Historic Jaipur",
      "activities": [
        {
          "time": "09:00",
          "name": "Amber Fort",
          "description": "Explore the fort complex and its courtyards.",
          "category": "Sightseeing",
          "notes": "Arrive early to avoid the busiest period."
        }
      ]
    }
  ]
}
```

Model-generated content varies, and the real response contains exactly three day objects for this request.

## Swagger

Run with `ASPNETCORE_ENVIRONMENT=Development`, open the `/swagger` URL printed by the application, expand `POST /api/travel/plan`, choose **Try it out**, enter a destination and a duration from 1 through 14, and execute. Swagger displays the nested `TripPlan`, `TripDay`, and `TripActivity` response schema. Empty destinations and out-of-range durations return `400 Bad Request` before the agent runs.

## Project layout

```text
chapter-02/
├── src/SmartTravelPlanner.Api/
│   ├── Agents/                    # Typed agent interface, implementation, and instructions
│   ├── Contracts/                 # Validated request contract
│   ├── Models/TravelPlanning/     # Structured output hierarchy
│   └── Controllers/               # Thin HTTP endpoint
├── tests/SmartTravelPlanner.Api.Tests/
├── Directory.Packages.props
└── SmartTravelPlanner.sln
```
