### Branch-First Draft PR + Aspire ServiceDefaults Implementation

**Summary**
- Create a fresh branch from `main`, save this plan to `plan.md`, and open a **draft PR first**.
- Implement Aspire `ServiceDefaults` + AppHost health integration on that PR.
- Canonicalize API project usage to `StudyCoach.BackendApi.csproj` and remove duplicate `Api.csproj`.

**Execution Sequence**
1. Create branch from `main` named `codex/aspire-servicedefaults-health`.
2. Add `plan.md` at repo root with this full implementation plan.
3. Commit plan-only change, push branch, open **draft PR** to `main`, and enable auto-merge if branch protections/checks allow.
4. Implement code changes on the same branch/PR.
5. Run verification commands and update PR description with test evidence.
6. Mark PR ready for review after all checks pass.

**Implementation Changes**
- Add new project: `backend/src/ServiceDefaults/StudyCoach.ServiceDefaults.csproj`.
- Add shared extensions file in that project with:
  - `AddServiceDefaults(IHostApplicationBuilder)` for Aspire template-parity defaults:
    - OpenTelemetry logs/metrics/traces
    - conditional OTLP exporter
    - service discovery
    - resilient default `HttpClient` configuration
    - health check registration
  - `MapDefaultEndpoints(WebApplication)` to expose `/health` and `/alive`.
- Wire API host:
  - Update `backend/src/Api/StudyCoach.BackendApi.csproj` with a project reference to ServiceDefaults.
  - Update `backend/src/Api/Program.cs` to call `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()`.
  - Keep existing controller/auth behavior unchanged.
- Wire Aspire AppHost health usage:
  - Update `backend/src/AppHost/Program.cs` to configure backend health probing against `/health`.
  - Keep frontend `WaitFor(backendApi)` so readiness depends on backend health.
- Solution/docs cleanup:
  - Add ServiceDefaults project to `StudyCoach.slnx`.
  - Delete `backend/src/Api/Api.csproj`.
  - Update `README.md` and any command docs to use only `backend/src/Api/StudyCoach.BackendApi.csproj`.

**Public Interfaces / Behavior**
- Added shared extension methods:
  - `AddServiceDefaults(IHostApplicationBuilder)`
  - `MapDefaultEndpoints(WebApplication)`
- Aspire will reflect backend health from endpoint checks instead of process-only status.

**Test Plan**
- `dotnet restore backend/src/Api/StudyCoach.BackendApi.csproj`
- `dotnet build backend/src/Api/StudyCoach.BackendApi.csproj`
- `dotnet build backend/src/AppHost/StudyCoach.AppHost.csproj`
- `dotnet test backend/tests/Api.Tests/Api.Tests.csproj`
- Run AppHost and verify:
  - backend shows healthy in Aspire
  - frontend waits for healthy backend before ready
  - `/health` and `/alive` succeed in Development
  - existing study endpoints/auth behavior remains intact

**Assumptions**
- Health probe path is `/health`.
- Full ServiceDefaults scope is required (telemetry + health + discovery + resilience).
- GitHub operations (`push`, `create PR`) require network access and authenticated `gh`/git context at execution time.
