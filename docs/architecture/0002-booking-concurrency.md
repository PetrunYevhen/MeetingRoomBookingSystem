# ADR 0002: Enforce one booking per slot with a unique index

- **Status:** Accepted
- **Date:** 2026-09-07

## Context

Several authenticated users can submit a booking for the same time slot at effectively the same time. The required result for otherwise valid requests is exactly one created booking: one request succeeds and every loser receives a clear conflict response. There must be no silent overwrite, double booking, or accidental HTTP 500.

A read that reports a slot as available is stale immediately after it completes. Therefore, the familiar sequence "query whether free, then insert" cannot protect the invariant by itself.

V1 has no cancellation and models `Booking.TimeSlotId` as required. A `TimeSlot` can have zero or one `Booking`.

## Decision drivers

- The invariant must hold across multiple API processes and Azure Web App instances.
- Correctness must survive retries, thread scheduling, and process boundaries.
- Conflict handling must be explicit and testable on both local SQL Server and Azure SQL.
- Lock duration and implementation complexity should remain small.
- No real-time notification may advertise uncommitted or failed state.

## Decision

Create the named unique database index `UX_Bookings_TimeSlotId` on `Booking.TimeSlotId`. Treat that index as the final authority for the one-booking-per-slot invariant.

The booking command directly attempts the insert inside a database transaction. It may read the referenced slot to validate that the slot exists and is bookable, but it must not rely on an availability pre-check for concurrency safety.

Conceptually, the command performs:

```text
authorize user
validate referenced slot
begin transaction
insert Booking(TimeSlotId, UserId, CreatedAtUtc)
save changes
commit transaction
publish SlotBookingChanged after commit
```

With two concurrent inserts for the same key, SQL Server's unique index permits only one committed row. One transaction obtains the unique key; the other either waits and then fails if the first commits, or can proceed if the first rolls back. No application-local coordination is needed, and the guarantee applies to every writer using the database.

The normal isolation level can remain `READ COMMITTED`: correctness comes from the unique index, not from an unprotected availability read or an assumed EF Core behavior. The explicit transaction defines the boundary for all booking-side database writes and ensures notification publication happens only after success.

## Failure translation

EF Core reports the losing insert as `DbUpdateException` with an inner SQL Server duplicate-key error (`2601` or `2627`, depending on how the provider reports the unique object). The persistence adapter translates an exception only when it identifies `UX_Bookings_TimeSlotId` as the violated index. Other unique constraints and other database exceptions follow their own mapping or remain server failures; they must not be disguised as a slot conflict.

The losing request rolls back and returns RFC 7807-compatible Problem Details:

- HTTP status: `409 Conflict`
- Content type: `application/problem+json`
- Problem type: `/problems/slot-already-booked`
- Extension: `"code": "slot_already_booked"`

Example:

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

The response deliberately does not reveal who owns the booking. A sequential request for an already-booked slot takes the same insert-and-map path; an availability read may be used for user experience but does not change the write guarantee.

## Commit and SignalR ordering

`SlotBookingChanged` is constructed from the successful result and handed to the `Realtime` module only after the database commit completes.

- Successful commit: return `201 Created` and publish one `booked` state notification for the resource group.
- Unique-index conflict: return `409 Conflict` and publish nothing.
- Rollback or unexpected database failure: publish nothing.
- SignalR failure after commit: do not roll back or misreport the already-committed booking; record the delivery failure. Clients refetch the REST schedule on reconnect and after mutation outcomes.

The event is idempotent state (`slotId` is `booked`) rather than a command, so applying a repeated delivery does not create another booking or expose user information.

## Alternatives considered

### Check availability, then insert without protection

Rejected. Two requests can both observe no booking before either inserts. This is the exact time-of-check/time-of-use race prohibited by the requirements.

### Application mutex or in-memory lock

Rejected. A lock protects only one process and fails as soon as the API scales to multiple instances. It also loses state on restart and is not authoritative for other database writers.

### Distributed lock

Rejected for V1. It adds another service, leases, expiry behavior, and failure modes while the database already owns the invariant. A distributed lock would still benefit from the unique index as a last line of defence.

### Serializable transaction with a pre-check or explicit row locking

Viable but not selected. Correct range/row locking can serialize contenders, but it holds locks longer, is easier to implement incorrectly, and still expresses the invariant less directly than a unique index. The unique index gives the required behavior with a single attempted write.

### Optimistic concurrency token (`rowversion`)

Rejected for this operation. A rowversion detects conflicting updates to the same existing row; competing requests create separate `Booking` rows. Without a unique key or a deliberately updated slot row, both inserts could succeed.

### Last-write-wins/upsert

Rejected. Replacing the first booking would be a silent overwrite and would violate both ownership and the one-winner contract.

## Verification strategy

An integration test will run against a real SQL Server container and the HTTP API:

1. Create one resource and one slot and authenticate the required callers.
2. Prepare multiple `POST /api/v1/bookings` requests for that same `timeSlotId`.
3. Release them together with a synchronization barrier so their write windows overlap.
4. Assert exactly one response is `201 Created`.
5. Assert every other response is `409 Conflict` with `code = slot_already_booked`.
6. Query the database through an independent scope and assert exactly one booking exists for the slot.
7. Assert no losing request produces a `SlotBookingChanged` notification.

The test must not use EF Core InMemory or SQLite because neither validates the chosen SQL Server error mapping and concurrency behavior. Unit tests can cover Problem Details mapping separately, but they do not replace the database integration test.

## Consequences

- Double booking remains impossible even when requests reach different API instances.
- The database schema visibly documents the business invariant.
- Duplicate-key translation is provider-specific and must be covered by SQL Server integration tests.
- The displayed availability is advisory; the booking response is authoritative.
- Future cancellation must remove or otherwise release the booking in a transaction before the event contract can start emitting `available`.
