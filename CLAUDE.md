# Repository guidance

## Project status

Feature-complete against the test task: authentication with roles, room/slot management,
concurrency-safe booking, real-time updates, and an Azure deployment. Treat the accepted
ADRs in `docs/architecture` as binding — change one by amending the ADR, not by working
around it in code.

## Structure

- `src/Backend` — ASP.NET Core 9 Minimal API host.
  - `Modules/*` — feature modules (`Auth`, `Resources`, `Bookings`, `Realtime`); each owns
    its entities, EF Core entity configurations, endpoints, contracts, and problem types.
  - `Infrastructure` — shared plumbing only (`DbContext`, migrations, seeding, RFC 7807
    helper, SQL Server exception translation). Never feature business rules.
- `src/Frontend` — Vite React TypeScript SPA.
- `tests/Backend.UnitTests` — fast, no I/O.
- `tests/Backend.IntegrationTests` — real SQL Server via Testcontainers; includes the
  concurrency test that is the point of the whole task.
- `docs/architecture` — accepted ADRs.
- `infra/provision.sh` — the exact script that created the Azure resources.

## Working conventions

- Use .NET SDK 9.0.308 as pinned by `global.json`; NuGet versions live in
  `Directory.Packages.props` (central package management, lock files committed).
- The API requires SQL Server: local dev via `docker-compose up -d sqlserver`, production
  via Azure SQL Database. `Development` auto-applies migrations at startup; every other
  environment gets `dotnet ef database update` from the deploy workflow.
- Seeding (roles, admin, sample rooms/slots) runs in every environment and must stay
  idempotent — the deployed app is the reviewer's entry point and cannot start empty.
- "One slot, one booking" is enforced by the unique index `UX_Bookings_TimeSlotId`
  (ADR 0002). Never replace it with a check-then-insert, and never widen the `catch` that
  translates its violation into `409 slot_already_booked`.
- `Bookings` must not reference SignalR — it publishes through the `IBookingNotifier`
  seam, and only after the transaction commits.
- Never allow credentialed CORS with wildcard origins.
- Keep frontend API URLs in Vite environment variables; do not hard-code deployed URLs.
- Do not commit secrets, `.env.local`, build output, or dependency directories.
- Commits are atomic and say what changed and why.

## Verification

Run these before handing off changes. `dotnet test` requires Docker: the integration tests
start their own SQL Server via Testcontainers, no manually running database needed. The
`/verify` skill in `.claude/skills` runs exactly this list.

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
