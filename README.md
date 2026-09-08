# Meeting Room Booking System

A booking system for a limited set of meeting rooms and their fixed time slots. Its central invariant is that a time slot can have at most one booking, even when several users submit requests for it at the same time. Successful changes are propagated to everyone viewing the affected room without a page refresh.

The repository is currently at **Stage 1: architecture**. It intentionally contains no application scaffolding, generated code, dependency lock files, or deployable artifacts yet. The original task is available in [Reenbit_TestTask_BookingConcurrency_EN 1.pdf](<./Reenbit_TestTask_BookingConcurrency_EN 1.pdf>).

## Planned technology stack

| Area | Technology |
| --- | --- |
| Backend | .NET 9, ASP.NET Core 9, EF Core, ASP.NET Core Identity, JWT bearer authentication, SignalR |
| Frontend | Vite, React, TypeScript, React Router, TanStack Query, MUI, SignalR client |
| Data | SQL Server for local development and integration tests; Azure SQL Database in production |
| Cloud | Separate Azure Web Apps for the API and SPA, plus Azure SignalR Service |
| Tests | .NET unit tests and integration tests against a real containerized SQL Server |

## Planned repository structure

Only the documentation exists at this stage. The implementation will follow this layout:

```text
.
├── docs/
│   └── architecture/
│       ├── 0001-system-architecture.md
│       └── 0002-booking-concurrency.md
├── src/
│   ├── Backend/                         # one ASP.NET Core modular-monolith project
│   │   ├── Modules/
│   │   │   ├── Auth/
│   │   │   ├── Resources/
│   │   │   ├── Bookings/
│   │   │   └── Realtime/
│   │   └── Infrastructure/              # shared EF Core and external-service adapters
│   └── Frontend/                        # Vite React SPA
└── tests/
    ├── Backend.UnitTests/
    └── Backend.IntegrationTests/
```

## Architecture at a glance

- The backend is one deployable ASP.NET Core modular monolith with explicit feature boundaries.
- ASP.NET Core Identity supplies users and the `User` and `Admin` roles.
- A short-lived access JWT authorizes API and SignalR calls. A rotating refresh token is kept in a `Secure`, `HttpOnly` cookie.
- One EF Core migration stream targets SQL Server locally and Azure SQL in production.
- A unique database index on `Booking.TimeSlotId` is the final concurrency guarantee. Duplicate-key failures for that index become an HTTP `409 Conflict`, not a server error.
- A booking status event is sent through SignalR only after the database commit succeeds. Events contain resource and slot identifiers, never user data.
- Production CORS allows credentials only from the single configured frontend origin.
- V1 supports creating bookings but not cancelling them.

See [ADR 0001: System architecture](docs/architecture/0001-system-architecture.md) for module, API, authentication, real-time, and deployment boundaries. See [ADR 0002: Booking concurrency](docs/architecture/0002-booking-concurrency.md) for the race-condition strategy and its alternatives.

## Fixed implementation assumptions

- Development uses .NET SDK 9.0.308, Node.js 24, and npm.
- The API and SPA build and deploy independently.
- Integration tests use an actual SQL Server container rather than EF Core InMemory or another database provider.
- `CLAUDE.md`, project scaffolding, and dependency lock files belong to the next implementation stage.
