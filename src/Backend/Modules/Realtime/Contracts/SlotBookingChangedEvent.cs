namespace MeetingRoomBooking.Api.Modules.Realtime.Contracts;

/// <summary>Wire shape for the `SlotBookingChanged` SignalR event, per ADR 0001.</summary>
public sealed record SlotBookingChangedEvent(Guid ResourceId, Guid SlotId, string Status, DateTime OccurredAtUtc);
