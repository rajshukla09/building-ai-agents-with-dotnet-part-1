# Building AI Agents with .NET

## Part 1: Microsoft Agent Framework, C#, and Production-Ready Agentic Workflows

Learn how to build **production-ready AI agents with .NET, C#, Microsoft Agent Framework, and Azure OpenAI** — from your first agent to structured outputs, persistent conversations, memory, tools, reliable execution, and agent workflows.

This is the official companion repository for **Building AI Agents with .NET — Part 1** and contains the complete runnable source code for Chapters 1–10.

📘 **Get the book on Amazon:**  
https://www.amazon.com/dp/B0HFMX5V9P

> ⭐⭐⭐⭐⭐ **“A must read for every CTO & Enterprise AI Engineer.”**  
> *“A great in-depth look at engineering production-grade agents.”*  
> — Verified Amazon Purchase

## What You'll Build

Throughout the book and this repository, you'll progressively build a production-oriented AI agent while exploring:

- **AI agents with Microsoft Agent Framework and C#**
- **Structured outputs** for predictable agent responses
- **Multi-turn conversations and agent sessions**
- **Persistent conversation state**
- **Tool-enabled agents**
- **Memory-aware agents**
- **Context providers**
- **Reliable tool execution**
- **Agent workflows**
- **Production-oriented .NET architecture**

Rather than presenting isolated examples, the chapters progressively evolve the same application so you can see how an agent moves from a simple implementation toward a more production-ready architecture.

## Chapter Map

| **Chapter** | **Topic** |
| ----------- | --------- |
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

Each `chapter-XX` directory is a **self-contained snapshot** of the application at that point in the book, including the solution, production source, relevant tests, and chapter-specific guidance.

Larger code listings may be abbreviated in the book for readability. The corresponding chapter directory contains the complete implementation.

## Prerequisites

- .NET 8 SDK
- An Azure OpenAI resource and model deployment
- Visual Studio, Visual Studio Code, Rider, or another .NET-compatible editor
- Git

Chapter 10 also includes a Blazor client.

See each chapter's README for its exact startup and testing instructions.

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

```bash
cd chapter-01/src/SmartTravelPlanner.Api

dotnet user-secrets set "AzureOpenAI:Endpoint" "https://YOUR-RESOURCE.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "YOUR-API-KEY"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "YOUR-DEPLOYMENT-NAME"
```

Repeat the configuration in the chapter you are running.

> **Security:** Never commit API keys, tokens, passwords, private endpoints, exported user secrets, or `.env` files.

## Build and Test

Each chapter is independent:

```bash
cd chapter-01

dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Replace `chapter-01` with the chapter you want to explore.

Start with that chapter's README because later chapters may expose additional endpoints, persistence mechanisms, clients, or workflow behavior.

---

# Continue with Part 2

## Building AI Agents with .NET — Part 2

**Advanced Agentic Architecture and Production Workflows**

Part 2 moves beyond the foundations into more advanced production agent architectures, including:

- Human-in-the-loop workflows
- Model Context Protocol (MCP)
- Multi-agent orchestration
- Durable workflows
- OpenTelemetry and observability
- Agent-to-Agent (A2A) communication
- Production-ready context engineering

📘 **Part 2 on Amazon:**  
https://www.amazon.com/dp/B0HJHDMCZB

💻 **Part 2 GitHub Companion Repository:**  
https://github.com/rajshukla09/building-ai-agents-with-dotnet-part-2

---

## About the Author

**Raj Shukla**

Author of the **Building AI Agents with .NET** series, focused on building practical, production-ready AI and agentic systems with .NET and Microsoft technologies.
