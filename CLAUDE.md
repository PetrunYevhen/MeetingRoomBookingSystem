# Repository guidance

## Project status

This repository is at Stage 2: runnable ASP.NET Core and React foundations. Business features, persistence, authentication, and real-time hubs are intentionally out of scope for this stage.

## Structure

- `src/Backend` contains the ASP.NET Core 9 Minimal API host.
- `src/Frontend` contains the Vite React TypeScript SPA.
- `tests/Backend.UnitTests` contains fast backend unit tests.
- `tests/Backend.IntegrationTests` contains API host integration tests.
- `docs/architecture` contains accepted architecture decisions that future changes must respect.

## Working conventions

- Use .NET SDK 9.0.308 as pinned by `global.json`.
- Persistence is introduced: the API requires SQL Server (local dev via `docker-compose up -d sqlserver`; production via Azure SQL Database). `Development` auto-applies migrations and seeds roles/sample data at startup; other environments require an explicit `dotnet ef database update`.
- Feature modules (`src/Backend/Modules/*`) own their entities and EF Core entity configurations; `src/Backend/Infrastructure` holds only shared EF Core plumbing (`DbContext`, migrations, seeding), never feature business rules.
- Never allow credentialed CORS with wildcard origins.
- Keep frontend API URLs in Vite environment variables; do not hard-code deployed URLs.
- Do not commit secrets, `.env.local`, build output, or dependency directories.

## Verification

Run these checks before handing off changes. `dotnet test` requires Docker: the integration tests start their own SQL Server via Testcontainers, no manually running database needed.

```sh
dotnet restore --locked-mode
dotnet build --no-restore
dotnet test --no-build
dotnet format --verify-no-changes --no-restore
npm --prefix src/Frontend ci
npm --prefix src/Frontend run lint
npm --prefix src/Frontend run format:check
npm --prefix src/Frontend run build
```
