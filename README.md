# Building AI Agents with .NET

## Part 1: Microsoft Agent Framework, C#, and Production-Ready Agentic Workflows

This repository contains the complete companion source code for Chapters 1–10 of *Building AI Agents with .NET*. Each `chapter-XX` directory is a self-contained snapshot of the application at that point in the book, including the solution, production source, relevant tests, and chapter-specific guidance.

Larger listings may be abbreviated in the book for readability. The corresponding chapter directory contains the complete implementation.

## Chapter map

| Chapter | Topic |
| --- | --- |
| 1 | Build Your First Agent |
| 2 | Structured Outputs with Microsoft Agent Framework |
| 3 | Conversations That Continue |
| 4 | Managing Agent Sessions |
| 5 | Persisting Conversations and State |
| 6 | Adding AI Tools to the Travel Agent |
| 7 | Building Memory-Aware Agents |
| 8 | Adding Context Providers |
| 9 | Reliable Tool Execution |
| 10 | Agent Workflows with Microsoft Agent Framework |

## Prerequisites

- .NET 8 SDK
- An Azure OpenAI resource and model deployment
- A code editor such as Visual Studio, Visual Studio Code, or Rider
- Git

Chapter 10 also includes a Blazor client. See each chapter's README for its exact startup and testing instructions.

## Configuration

Every API project contains safe placeholders in `appsettings.json`:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "ApiKey": "",
    "DeploymentName": "YOUR-DEPLOYMENT-NAME"
  }
}
```

For local development, prefer .NET user secrets instead of editing tracked configuration:

```powershell
cd chapter-01/src/SmartTravelPlanner.Api
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://YOUR-RESOURCE.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "YOUR-API-KEY"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "YOUR-DEPLOYMENT-NAME"
```

Repeat the configuration in the chapter you are running. Never commit API keys, tokens, passwords, private endpoints, exported user secrets, or `.env` files.

## Build and test

Each folder is independent:

```powershell
cd chapter-01
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Replace `chapter-01` with the chapter you want to explore. Begin with the chapter README because later chapters may expose additional endpoints, persistence, clients, or workflow behavior.

## Part 2

Part 2 continues beyond these foundations into more advanced agentic architecture and production scenarios. Its source will be published separately.

## Author

Raj Shukla
