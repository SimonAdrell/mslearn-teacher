# Study Coach (Foundry Agent Interface)

This repository contains a v1 implementation of an AI-102 Study Coach interface:
- `backend/src/Api`: ASP.NET Core API with Entra auth, study endpoints, coach_meta parsing, and Learn citation gate.
- `backend/src/AppHost`: Aspire AppHost for local orchestration.
- `backend/tests/Api.Tests`: parser, provider, and endpoint tests.
- `frontend`: React + TypeScript + Vite web app.
- `docs/foundry-agent-prompt.md`: recommended Azure Foundry prompt contract (hybrid prose + `coach_meta`).

Foundry communication in the API uses `Azure.AI.Projects` (and `Azure.AI.Agents.Persistent` for persistent agent threads/runs).

## Run backend

```powershell
dotnet restore StudyCoach.slnx
dotnet build StudyCoach.slnx
dotnet test backend/tests/Api.Tests/Api.Tests.csproj
dotnet run --project backend/src/Api/StudyCoach.BackendApi.csproj
```

## Run frontend

```powershell
cd frontend
npm install
npm run dev
```

Copy `frontend/.env.example` to `.env.local` and set Entra values for your tenant/app registration.

To call your real Foundry agent instead of mock mode, set:
- `Foundry:UseMockResponses=false`
- `Foundry:ProjectEndpoint=<your AI Foundry project endpoint>`
- `Foundry:AgentName=<your Foundry agent name>`
- `Foundry:AgentVersion=<your Foundry agent version>`

To enable MCP-backed skills-outline refresh, set:
- `LearnMcp:BaseUrl=<your Learn MCP bridge base URL>`
- Optional: `LearnMcp:SkillsOutlinePath=/ai-102/skills-outline`
- Optional: `LearnMcp:CacheHours=24`
