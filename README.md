# Meeting Room Booking System

A booking system for a limited set of meeting rooms and their fixed time slots. Its central invariant is that a time slot can have at most one booking, even when several users submit requests concurrently.

The repository is at **Stage 7: realtime updates**. It contains a locally runnable ASP.NET Core 9 Minimal API backed by EF Core/SQL Server, a working Vite React TypeScript SPA (login, room list, room schedule with booking), and backend unit and integration test projects. The `Auth` module exposes register/login/refresh/logout/me behind role-based JWT authorization, the `Resources` module exposes rooms/schedule browsing plus admin CRUD, `POST /api/v1/bookings` guarantees exactly one `201 Created` and every other request a `409 slot_already_booked` under concurrent load (see [ADR 0002](docs/architecture/0002-booking-concurrency.md)), and the `Realtime` module now pushes a `SlotBookingChanged` SignalR event to everyone viewing that room the instant a booking commits — every open client sees the status change without refreshing. `GET /api/v1/admin/bookings` and Azure deployment (including Azure SignalR Service) are deferred to later stages.

The original task is available in [Reenbit_TestTask_BookingConcurrency_EN 1.pdf](<./Reenbit_TestTask_BookingConcurrency_EN 1.pdf>).

## Technology foundation

| Area | Current foundation |
| --- | --- |
| Backend | .NET 9, ASP.NET Core Minimal API, EF Core 9 + SQL Server, ASP.NET Core Identity, JWT bearer auth + role-based authorization policies, ASP.NET Core SignalR, OpenAPI, Swagger UI, explicit credentialed CORS |
| Frontend | Vite, React, TypeScript, React Router, TanStack Query, MUI, SignalR client — login, room list, live room schedule |
| Tests | xUnit unit tests (incl. EF Core model assertions and JWT claim/expiry checks) and `WebApplicationFactory` integration tests against a real SQL Server via Testcontainers, including a real SignalR client against the test host |
| Tooling | .NET SDK pin, NuGet Central Package Management (`Directory.Packages.props`) + lock files, npm lock file, EditorConfig, ESLint, Prettier, `dotnet-ef` local tool |

## Repository structure

```text
.
├── MeetingRoomBookingSystem.sln
├── docker-compose.yml
├── global.json
├── docs/architecture/
├── src/
│   ├── Backend/
│   │   ├── Configuration/
│   │   ├── Infrastructure/
│   │   │   └── Persistence/
│   │   │       ├── Migrations/
│   │   │       └── Seed/
│   │   ├── Modules/
│   │   │   ├── Auth/
│   │   │   ├── Resources/
│   │   │   ├── Bookings/
│   │   │   └── Realtime/
│   │   ├── Properties/
│   │   ├── MeetingRoomBooking.Api.csproj
│   │   └── Program.cs
│   └── Frontend/
│       ├── src/
│       │   ├── api/          # REST calls (auth, resources, bookings)
│       │   ├── auth/         # AuthContext/AuthProvider, ProtectedRoute
│       │   ├── realtime/     # singleton HubConnection
│       │   ├── hooks/        # useResourceSlotsRealtime
│       │   └── pages/
│       ├── package.json
│       └── vite.config.ts
└── tests/
    ├── Backend.UnitTests/
    └── Backend.IntegrationTests/
```

The planned feature boundaries and deployment model are described in [ADR 0001: System architecture](docs/architecture/0001-system-architecture.md). The booking race-condition strategy is described in [ADR 0002: Booking concurrency](docs/architecture/0002-booking-concurrency.md).

## Prerequisites

- .NET SDK 9.0.308 (pinned by `global.json`)
- Node.js 24
- npm 12
- Docker (local SQL Server via `docker-compose`; integration tests start their own container via Testcontainers)

## Install dependencies

From the repository root:

```sh
dotnet restore --locked-mode
dotnet tool restore
npm --prefix src/Frontend ci
```

## Configuration

The tracked development settings connect the SPA and API over local HTTP:

| Application | Setting | Development value |
| --- | --- | --- |
| Frontend | `VITE_API_BASE_URL` | `http://localhost:5080` |
| Backend | `Cors:AllowedOrigins:0` | `http://localhost:5173` |
| Backend | `ConnectionStrings:Default` | Points at the `docker-compose` SQL Server (see [Database](#database)) |
| Backend | `Jwt:SigningKey`, `Admin:Email`/`Admin:Password` | Tracked dev-only defaults (see [Authentication](#authentication)) |

`src/Frontend/.env.development` supplies the local frontend value. Copy `src/Frontend/.env.example` to an ignored local override when needed; never commit secrets or `.env.local` files.

For production, set the frontend URL during the Vite build and configure the API with an exact origin:

```text
VITE_API_BASE_URL=https://api.example.com
Cors__AllowedOrigins__0=https://app.example.com
```

The API rejects missing, empty, wildcard, non-HTTP(S), and path-bearing CORS origins during startup. Credentialed requests are enabled only for configured exact origins. Non-development API traffic is redirected to HTTPS.

## Database

Local development uses SQL Server in Docker; production uses Azure SQL Database (see [ADR 0001](docs/architecture/0001-system-architecture.md)). The `Development` connection string and its SA password in `appsettings.Development.json` and `docker-compose.yml` are throwaway local-only credentials — they protect nothing and must never be reused anywhere else.

Start the local database:

```sh
docker compose up -d sqlserver
```

`dotnet run` in the `Development` environment automatically applies pending migrations and seeds the `User`/`Admin` roles plus sample rooms and time slots on startup — no manual step needed for local development. Integration tests never touch this container: they start and dispose their own SQL Server via Testcontainers.

To manage migrations directly (e.g. after changing an entity or its configuration):

```sh
dotnet tool restore
dotnet ef migrations add <Name> --project src/Backend/MeetingRoomBooking.Api.csproj
dotnet ef database update --project src/Backend/MeetingRoomBooking.Api.csproj
```

## Authentication

The `Auth` module (see [ADR 0001](docs/architecture/0001-system-architecture.md#authentication-and-authorization)) issues a short-lived JWT access token in the response body and an opaque refresh token in an `HttpOnly`/`Secure`/`SameSite=None` cookie scoped to `/api/v1/auth`, with rotation and reuse detection.

| Route | Access |
| --- | --- |
| `POST /api/v1/auth/register` | Anonymous — always creates a `User`, never `Admin` |
| `POST /api/v1/auth/login` | Anonymous — returns the access token and sets the refresh cookie |
| `POST /api/v1/auth/refresh` | Refresh cookie + matching `Origin` header — rotates both |
| `POST /api/v1/auth/logout` | Refresh cookie + matching `Origin` header — revokes the session |
| `GET /api/v1/auth/me` | `Authorization: Bearer` |

`Jwt:SigningKey` and `Admin:Email`/`Admin:Password` in `appsettings.Development.json` are tracked dev-only defaults, same convention as the SQL SA password — `dotnet run` bootstraps a working admin account (`admin@meetingroombooking.local`) with zero extra setup. An administrator is never created from client input; production provisioning is a deliberate deploy-time step, not yet built (no deployment pipeline exists at this stage).

## Resources & slots

The `Resources` module (see [ADR 0001](docs/architecture/0001-system-architecture.md#resources-and-bookings)) is browsable by any authenticated user and managed by admins. Deleting a room that still has slots, or editing/deleting a slot that already has a booking, is rejected as a `409` — the DB's `DeleteBehavior.Restrict` foreign keys are the actual authority, translated to `resource_has_slots` / `timeslot_has_booking` Problem Details.

| Route | Access |
| --- | --- |
| `GET /api/v1/resources` | `User` or `Admin` |
| `GET /api/v1/resources/{resourceId}` | `User` or `Admin` |
| `GET /api/v1/resources/{resourceId}/slots?fromUtc=&toUtc=` | `User` or `Admin` — each slot's `status` is `available`/`booked` |
| `POST /api/v1/admin/resources`, `PUT /admin/resources/{resourceId}`, `DELETE /admin/resources/{resourceId}` | `Admin` |
| `POST /api/v1/admin/resources/{resourceId}/slots`, `PUT /admin/slots/{slotId}`, `DELETE /admin/slots/{slotId}` | `Admin` |

`GET /api/v1/admin/bookings` isn't implemented yet.

## Bookings

`POST /api/v1/bookings` (`{ "timeSlotId": "..." }`, `User` or `Admin`) is the task's central requirement (see [ADR 0002](docs/architecture/0002-booking-concurrency.md)): the unique index `UX_Bookings_TimeSlotId` — not a check-then-insert in application code — is the sole authority for "one slot, one booking." The endpoint validates the slot exists, inserts inside an explicit transaction, and commits; a losing concurrent insert is translated from the SQL Server unique-index violation into:

```json
{ "type": "/problems/slot-already-booked", "title": "The time slot is already booked.", "status": 409,
  "detail": "Another booking was committed for the requested time slot.", "instance": "/api/v1/bookings", "code": "slot_already_booked" }
```

A `SlotBookingChanged` notification is published only after commit, through an `IBookingNotifier` seam (`Modules/Bookings`) that keeps the module free of any direct dependency on SignalR — `SignalRBookingNotifier` (`Modules/Realtime`) is the concrete implementation, swapped in via DI only.

## Realtime

The authenticated SignalR hub at `/hubs/bookings` (see [ADR 0001](docs/architecture/0001-system-architecture.md#real-time-boundary)) groups connections per resource: a client calls `WatchResource(resourceId)` (server-validated, group name never client-chosen) and `UnwatchResource(resourceId)` when it stops viewing that room. After a booking commits, every connection watching that resource receives:

```json
{ "resourceId": "...", "slotId": "...", "status": "booked", "occurredAtUtc": "..." }
```

No event is ever published for a losing/rolled-back booking attempt — `BookingEndpoints` only calls the notifier after `transaction.CommitAsync()` succeeds. The SPA's `RealtimeProvider` keeps one hub connection for the whole session (`accessTokenFactory` supplies the bearer token, since browsers can't set headers on a WebSocket handshake) with automatic reconnect; `useResourceSlotsRealtime` patches the specific slot in the TanStack Query cache on the event and refetches the whole schedule on reconnect (ADR 0001: "SignalR is a freshness channel, not the source of truth"). Locally this is the plain ASP.NET Core hub; Azure SignalR Service is deploy-stage work, not yet wired up.

## Run locally

Start the database, then the API and SPA in separate terminals from the repository root:

```sh
docker compose up -d sqlserver
dotnet run --project src/Backend/MeetingRoomBooking.Api.csproj --launch-profile http
```

```sh
npm --prefix src/Frontend run dev
```

Local interfaces:

- SPA: `http://localhost:5173` — sign in with the seeded admin (`admin@meetingroombooking.local` / `Local_Dev_Only_Admin_P@ss1`, see [Authentication](#authentication)), browse a room, book a slot
- API health check page: `http://localhost:5173/health`
- API health: `http://localhost:5080/health`
- OpenAPI JSON: `http://localhost:5080/openapi/v1.json`
- Swagger UI (Development only): `http://localhost:5080/swagger`

To see a live update, register a second account (`POST /api/v1/auth/login`'s sibling `/register`, or via Swagger), open the same room in two browser windows signed in as two different users, and book a slot in one — the other updates without a refresh.

## Verify

`dotnet test` requires Docker: the integration tests start their own SQL Server container via Testcontainers, so the `docker-compose` database above is not required to run them.

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

The backend unit tests cover startup CORS-origin validation, the EF Core model's schema guarantees (the `UX_Bookings_TimeSlotId` unique index, the slot-duplication and refresh-token-hash unique indexes, FK delete behavior, and the start-before-end check constraint), and JWT claim/expiry contents. Integration tests cover health, OpenAPI, the allowed frontend origin, suppression of CORS headers for an untrusted origin, that migrations apply cleanly against a real SQL Server, the full auth flow — register/login/me, the refresh `Origin` check, refresh-token rotation and reuse-detection revoking a whole token family, logout, and admin-bootstrap login — the Resources module (browsing, the `User`/`Admin` authorization boundary, schedule status/date filtering, and every flagged conflict), the booking concurrency guarantee: **`BookingEndpointsTests.CreateBooking_ConcurrentRequestsForSameSlot_ExactlyOneSucceeds`** fires 15 real concurrent `POST /api/v1/bookings` requests at the same slot through a `Barrier`, asserts every response is `201`/`409` (explicitly ruling out a `500`), exactly one `201`, fourteen `409 slot_already_booked`, exactly one `Booking` row in the database, and exactly one notification fired — and the Realtime module: `RealtimeTests` connects a real SignalR client to the test host and proves a `WatchResource`-subscribed connection actually receives `SlotBookingChanged` with the correct payload when a booking is created through HTTP, that a connection watching a different resource does not, that connecting without a token is rejected, and that watching an unknown resource throws. Manually verified end-to-end in a real browser too: two signed-in users viewing the same room, one books, the other's screen updates with no refresh.

## Deploy to Azure

| Resource | SKU | Region |
| --- | --- | --- |
| Resource group `rg-meeting-room-booking` | — | Sweden Central |
| SQL Server `sql-mrb-9b8875` + Database `sqldb-meetingroombooking` | Basic, 5 DTU | Sweden Central |
| SignalR Service `signalr-mrb-9b8875` | Free_F1, Default mode | Sweden Central |
| App Service Plan `asp-mrb-9b8875` + Web App `api-mrb-9b8875` (backend) | F1 Free | Sweden Central |
| Application Insights `appi-mrb-9b8875` | codeless auto-instrumentation on the backend Web App | Sweden Central |
| Static Web App `swa-mrb-9b8875` (frontend) | Free | West Europe |

**Live URLs**: backend `https://api-mrb-9b8875.azurewebsites.net`, frontend `https://brave-moss-0cd9c9e03.3.azurestaticapps.net`.

**Region rationale**: this subscription's Free Trial quota blocks Basic-tier SQL provisioning in West Europe/North Europe; Sweden Central has no such restriction and offers the same SKUs. Static Web Apps aren't offered in Sweden Central at all, so the frontend uses the nearest supported region (West Europe) — that's just metadata placement, not a latency concern, since Static Web Apps serve through a global CDN regardless.

**Frontend hosting deviates from ADR 0001's original wording** ("Deploy the API and SPA as separate Azure Web Apps") — the ADR has been updated to Static Web App, with the rationale recorded there: free tier, built-in SPA fallback routing, native GitHub Actions deploy action, versus paying for and hand-configuring a second Web App plan just to serve static files.

**Free F1 tier trade-off**: the backend Web App has no "Always On" — it unloads after ~20 minutes idle, so the first request after a quiet period takes an extra 10-30s to cold-start. Hit `/health` once before a live demo to warm it up.

### Infrastructure

`infra/provision.sh` is the exact, commented `az` script used to create every resource above and wire up GitHub's OIDC federated identity — read it top to bottom to see precisely what exists and why, or re-run it (with `MRB_SUFFIX` pinned) to reproduce the setup from scratch.

### CI/CD

Two GitHub Actions workflows:

- **`.github/workflows/ci.yml`** — on every push/PR to `main`: the same backend (`dotnet restore --locked-mode` / `build` / `test` / `format --verify-no-changes`) and frontend (`npm ci` / `lint` / `format:check` / `build`) checks as the [Verify](#verify) section above. A required gate, not a deploy.
- **`.github/workflows/deploy.yml`** — on push to `main` (or manual `workflow_dispatch`), two independent jobs:
  - `deploy-backend`: publishes the API, authenticates to Azure via OIDC (no stored Azure credential — an App Registration with a federated credential trusting GitHub's OIDC issuer, `Contributor` scoped to only this resource group), opens a firewall rule for the runner's own IP, runs `dotnet ef database update` against the production database (the "controlled deploy-time migration" — the app itself never auto-migrates outside Development), closes the firewall rule, then zip-deploys to the Web App.
  - `deploy-frontend`: builds the SPA with `VITE_API_BASE_URL` pointed at the backend, deploys `dist/` to the Static Web App using its deployment token.

Bootstrapping a new environment needs these GitHub secrets/variables (all set by `infra/provision.sh`): secrets `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_SQL_CONNECTION_STRING`, `AZURE_STATIC_WEB_APPS_API_TOKEN`; variables `VITE_API_BASE_URL`, `AZURE_SQL_SERVER_NAME`, `AZURE_WEBAPP_NAME`, `AZURE_RESOURCE_GROUP`.

To run a migration by hand instead of through CI:

```sh
dotnet ef database update \
  --project src/Backend/MeetingRoomBooking.Api.csproj \
  --connection "<production connection string>"
```

### Teardown

Once review is finished, delete everything to stop the SQL Basic-tier charge:

```sh
az group delete --name rg-meeting-room-booking --yes
az ad app delete --id "$(az ad app list --display-name gh-actions-mrb-deploy --query '[0].appId' -o tsv)"
```
