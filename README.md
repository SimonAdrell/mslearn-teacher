# Study Coach (Foundry Agent Interface)

This repository contains a v1 implementation of an AI-102 Study Coach interface:
- `backend/src/Api`: ASP.NET Core API with Entra auth, study endpoints, and citation gate.
- `backend/src/AppHost`: Aspire AppHost for local orchestration.
- `backend/tests/Api.Tests`: endpoint/integration tests.
- `frontend`: React + TypeScript + Vite web app.

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
- `Foundry:AgentId=<your persistent agent id>`
