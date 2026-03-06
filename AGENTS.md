# Repository Guidelines

## Project Structure & Module Organization
Use a split architecture for backend and frontend:
- `backend/` ASP.NET Core Web API (`*.sln`, `src/`, `tests/`).
- `frontend/` React app (`src/`, `public/`, `tests/`).
- `docs/` architecture notes, ADRs, and API contracts.
- `infra/` local/dev infrastructure scripts (for example Docker Compose, IaC templates).

Recommended backend layout:
- `backend/src/Api` (controllers/endpoints)
- `backend/src/Application` (use cases/services)
- `backend/src/Domain` (entities/value objects)
- `backend/src/Infrastructure` (data access, external clients)
- `backend/tests` (unit + integration tests)

## Build, Test, and Development Commands
Run commands from repo root unless noted:
- `dotnet restore backend/*.sln` restores backend dependencies.
- `dotnet build backend/*.sln` builds all C# projects.
- `dotnet test backend/tests` runs backend test suites.
- `npm --prefix frontend install` installs frontend dependencies.
- `npm --prefix frontend run dev` starts React dev server.
- `npm --prefix frontend run build` creates production frontend build.
- `npm --prefix frontend test` runs frontend tests.

## Coding Style & Naming Conventions
Backend (C#):
- 4-space indentation; `PascalCase` for types/methods, `camelCase` for locals/params, `_camelCase` for private fields.
- One public type per file; file name matches type name.
- Prefer async APIs (`Task`/`Task<T>`) for I/O paths.
- Use controller-based APIs (MVC controllers) instead of minimal APIs for HTTP endpoints in this repository.

Frontend (React/TypeScript):
- Components in `PascalCase` files (for example `StudyPanel.tsx`).
- Hooks in `useXxx.ts`; utilities in `camelCase.ts`.
- Keep presentational and data-fetch logic separated.

Use formatters/linters: `dotnet format` for C#, `eslint` + `prettier` for frontend.

## Testing Guidelines
- Backend: xUnit with FluentAssertions; test files end with `Tests.cs`.
- Frontend: Vitest or Jest + React Testing Library; test files `*.test.ts(x)`.
- Add tests for each new feature and bug fix, including edge cases and error paths.

## Commit & Pull Request Guidelines
- Commit messages: imperative, concise (for example, `Add AI-102 quiz endpoint`).
- Keep commits scoped to one concern.
- Create PRs with auto-merge enabled whenever branch protections and required checks allow it.
- PRs must include purpose, linked issue, test evidence (`dotnet test`, `npm test`), and screenshots for UI changes.

## Security & Configuration Tips
- Never commit secrets. Use `appsettings.Development.json` (ignored) and `frontend/.env.local`.
- Keep CORS, auth settings, and API base URLs environment-specific.
- Document required environment variables in `README.md`.
