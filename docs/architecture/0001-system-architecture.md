# ADR 0001: System architecture

- **Status:** Accepted
- **Date:** 2026-09-07

## Context

The system manages a limited set of meeting rooms, each with a fixed set of bookable time slots. Regular users can inspect schedules and book an available slot. Administrators can manage resources and slots and inspect all bookings. When booking state changes, everyone viewing that resource must receive the change in real time.

The defining correctness requirement is that concurrent requests must never create two bookings for one slot. The solution must run on Azure with an ASP.NET Core backend, Azure SQL Database, and Azure SignalR Service. The backend and frontend are independently deployable.

## Decision

Build one ASP.NET Core 9 modular-monolith backend, one Vite React TypeScript SPA, and one relational database. Keep the backend features in explicit modules while sharing a single host, EF Core `DbContext`, and migration stream. Deploy the API and SPA as separate Azure Web Apps.

This gives the project clear internal ownership without introducing distributed transactions or operational overhead between backend services. Module boundaries are code boundaries; deployment and database consistency remain those of one application.

## Planned solution layout

```text
src/
├── Backend/
│   ├── Modules/
│   │   ├── Auth/
│   │   ├── Resources/
│   │   ├── Bookings/
│   │   └── Realtime/
│   └── Infrastructure/
└── Frontend/

tests/
├── Backend.UnitTests/
└── Backend.IntegrationTests/
```

`src/Backend` is one ASP.NET Core project and the composition root. Each feature keeps its endpoints, application logic, domain types, and persistence configuration together. `Infrastructure` contains the shared EF Core setup and adapters for external services; it must not become a home for feature business rules.

## Backend module boundaries

| Module | Owns | May depend on |
| --- | --- | --- |
| `Auth` | Identity users and roles, login/registration, refresh-token sessions, JWT issuance | Shared infrastructure |
| `Resources` | Meeting rooms (`Resource`) and their fixed `TimeSlot` records; public schedule queries; admin resource/slot management | Authenticated principal and shared infrastructure |
| `Bookings` | `Booking`, booking creation, current-user/admin booking queries, conflict translation | Resource contracts, authenticated user identifier, shared infrastructure |
| `Realtime` | `/hubs/bookings`, resource viewer groups, and post-commit `SlotBookingChanged` delivery | Read-only resource contract and committed booking notifications |

The composition root wires modules together. A module can use another module's explicit contract but not its internal handlers or persistence types. Dependencies flow from `Bookings` to the minimal `Resources` contract and from `Realtime` to committed booking notifications; there is no dependency back from either module, preventing cycles.

## Persistence boundary

- Local development uses SQL Server; production uses Azure SQL Database.
- The backend owns one EF Core `DbContext`, one migrations assembly, and one ordered migration history for Identity and all feature tables.
- Feature modules own their entity configurations even though the context and transaction infrastructure are shared.
- `Booking.TimeSlotId` is required and has the named unique index `UX_Bookings_TimeSlotId`.
- Foreign keys enforce valid user, resource, slot, and booking relationships. Database-generated and audit timestamps are stored as UTC.
- Integration tests exercise the SQL Server provider in a container. EF Core InMemory is not an acceptable substitute for relational/concurrency tests.

The database invariant and transaction behavior are specified in [ADR 0002](0002-booking-concurrency.md).

## HTTP API boundary

All REST endpoints are under `/api/v1`, use JSON, and return validation or failure information as RFC 7807-compatible `application/problem+json`. Identifiers below are placeholders for the implementation's opaque identifier type.

### Authentication

| Method and route | Access | Contract |
| --- | --- | --- |
| `POST /api/v1/auth/register` | Anonymous | Creates a regular user; a caller cannot select the `Admin` role |
| `POST /api/v1/auth/login` | Anonymous | Validates credentials, returns an access JWT, and sets a refresh-token cookie |
| `POST /api/v1/auth/refresh` | Refresh cookie | Rotates the refresh token and returns a new access JWT |
| `POST /api/v1/auth/logout` | Current session | Revokes the refresh-token session and clears its cookie |
| `GET /api/v1/auth/me` | `User` or `Admin` | Returns the current user's public profile and roles |

### Resources and bookings

| Method and route | Access | Contract |
| --- | --- | --- |
| `GET /api/v1/resources` | `User` or `Admin` | Lists meeting rooms |
| `GET /api/v1/resources/{resourceId}` | `User` or `Admin` | Returns room details |
| `GET /api/v1/resources/{resourceId}/slots` | `User` or `Admin` | Returns the room's slots and current availability, optionally filtered by a UTC date range |
| `POST /api/v1/bookings` | `User` or `Admin` | Creates a booking from `{ "timeSlotId": "..." }`; returns `201 Created` or the defined slot conflict |

V1 does not expose booking cancellation. A slot can transition from available to booked only.

### Administration

| Method and route | Access | Contract |
| --- | --- | --- |
| `POST /api/v1/admin/resources` | `Admin` | Creates a room |
| `PUT /api/v1/admin/resources/{resourceId}` | `Admin` | Replaces editable room details |
| `DELETE /api/v1/admin/resources/{resourceId}` | `Admin` | Removes a room when domain rules permit |
| `POST /api/v1/admin/resources/{resourceId}/slots` | `Admin` | Creates a fixed bookable slot for the room |
| `PUT /api/v1/admin/slots/{slotId}` | `Admin` | Replaces editable slot details when domain rules permit |
| `DELETE /api/v1/admin/slots/{slotId}` | `Admin` | Removes an unbooked slot |
| `GET /api/v1/admin/bookings` | `Admin` | Lists bookings across users with filtering and pagination |

Domain conflicts from admin operations also use intentional `409` Problem Details responses. They must not be mistaken for the booking unique-index conflict.

### Slot conflict response

A request that loses the booking race returns `409 Conflict` with a stable machine-readable extension:

```json
{
  "type": "/problems/slot-already-booked",
  "title": "The time slot is already booked.",
  "status": 409,
  "detail": "Another booking was committed for the requested time slot.",
  "instance": "/api/v1/bookings",
  "code": "slot_already_booked"
}
```

No username, email address, or other information about the winning user is disclosed.

## Authentication and authorization

ASP.NET Core Identity stores password hashes, users, and the `User` and `Admin` roles. Public registration always creates `User`; administrators are provisioned through a controlled deployment/seed process rather than from client input.

On login, the API returns a short-lived signed access JWT in the response body. The SPA keeps it in memory and sends it in the `Authorization: Bearer` header for REST and authenticated SignalR negotiation. The JWT contains only the stable subject identifier and required role claims; secrets and personal profile data are excluded.

The refresh token is an opaque, high-entropy value. Only its hash and session metadata are stored server-side. The plaintext token is set as a cookie with `Secure`, `HttpOnly`, and `SameSite=None`, and is never exposed to JavaScript. Refresh rotates the token: the used token is revoked and replaced, and detected reuse revokes its token family. Logout revokes the active session and expires the cookie.

Because the API and SPA have different origins, the SPA explicitly uses credentialed requests for refresh and logout. The API:

- allows exactly the configured frontend origin, methods, and headers;
- enables credentials for that origin and never combines credentials with wildcard origins;
- requires the production `Origin` header on cookie-authenticated state-changing requests and rejects missing or non-allowlisted origins;
- uses HTTPS for all production traffic.

Authorization policies enforce `Admin` on administration endpoints and either `User` or `Admin` on resource viewing and booking. UI visibility is only a convenience; the API remains the authority.

## Real-time boundary

The authenticated SignalR hub is exposed at `/hubs/bookings`. In production, the backend uses Azure SignalR Service; local development can use the ASP.NET Core hub directly.

After connecting, a client asks the hub to watch a resource. The server validates the resource and adds that connection to a server-controlled group named from the resource identifier. Disconnecting automatically removes the connection. Clients cannot select an arbitrary delivery group name.

The server sends this event to the affected resource group:

```text
Event: SlotBookingChanged
Payload:
{
  "resourceId": "...",
  "slotId": "...",
  "status": "booked",
  "occurredAtUtc": "2026-09-07T12:34:56.789Z"
}
```

The contract permits `available` as a future status, but V1 emits only `booked` because cancellation is out of scope. The event is state-oriented and safe to apply more than once. It carries no booking owner or other personal data. On reconnect, or if live delivery fails, the SPA refetches the schedule through REST; SignalR is a freshness channel, not the source of truth.

## Booking and notification flow

1. The SPA sends `POST /api/v1/bookings` with an access JWT and `timeSlotId`.
2. `Bookings` validates authentication and the existence/bookability of the referenced slot. It does not use a prior availability query as its concurrency guard.
3. The API inserts the booking inside a database transaction.
4. Azure SQL/SQL Server enforces `UX_Bookings_TimeSlotId`. A losing concurrent insert is translated to the documented `409`; unrelated database errors are not relabelled as conflicts.
5. The transaction commits. Only after commit does `Bookings` hand the state-change notification to `Realtime`.
6. `Realtime` publishes `SlotBookingChanged` through Azure SignalR Service to the resource group.
7. Each SPA updates or invalidates the matching TanStack Query cache. REST remains authoritative.

If commit fails, no SignalR event is published. If real-time publication fails after commit, the booking remains valid, the failure is logged/observed, and clients converge through reconnect/refetch behavior.

## Frontend boundary

The Vite React application uses:

- React Router for public/authenticated/admin routes;
- TanStack Query for REST server state, cache invalidation, and reconnect refetches;
- MUI for accessible visual components;
- the SignalR JavaScript client for authenticated resource subscriptions.

The frontend does not decide booking availability or resolve concurrent writes. It renders the server's state, treats `409` as an expected business outcome, and refreshes the affected resource after either a REST mutation or a SignalR event.

## Deployment topology

```text
Browser
  ├── HTTPS static application ─────────────> Azure Web App: SPA
  ├── HTTPS REST + credentials/JWT ─────────> Azure Web App: API ───> Azure SQL Database
  └── negotiate via API, then WebSocket ────> Azure SignalR Service <── API SignalR SDK
```

The API Web App is the only component with database credentials and Azure SignalR connection settings. JWT signing material, SQL connection strings, and service credentials are supplied through protected Azure configuration (and Key Vault references where configured), never source control. The API's CORS configuration contains the deployed SPA origin. Health endpoints and telemetry belong to the API deployment; neither exposes secrets.

## Consequences

- One backend deployment and one migration stream keep implementation and transactions straightforward.
- Feature ownership remains reviewable and can be separated later if scale or team boundaries justify it.
- Independent API and SPA deployment requires explicit CORS, cookie, and environment configuration.
- SignalR improves freshness but does not replace REST reads or database constraints.
- The SQL Server provider is part of correctness, so concurrency integration tests must run against the real engine.
