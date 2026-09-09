import { apiFetch } from './httpClient'
import type { ResourceDto, TimeSlotDto } from './resourcesApi'

export interface AdminBookingDto {
  id: string
  resourceId: string
  resourceName: string
  timeSlotId: string
  startUtc: string
  endUtc: string
  userId: string
  userEmail: string
  createdAtUtc: string
}

export function listAllBookings(
  accessToken: string,
): Promise<AdminBookingDto[]> {
  return apiFetch<AdminBookingDto[]>('/api/v1/admin/bookings', {}, accessToken)
}

export function createResource(
  name: string,
  accessToken: string,
): Promise<ResourceDto> {
  return apiFetch<ResourceDto>(
    '/api/v1/admin/resources',
    { method: 'POST', body: JSON.stringify({ name }) },
    accessToken,
  )
}

export function updateResource(
  resourceId: string,
  name: string,
  accessToken: string,
): Promise<ResourceDto> {
  return apiFetch<ResourceDto>(
    `/api/v1/admin/resources/${resourceId}`,
    { method: 'PUT', body: JSON.stringify({ name }) },
    accessToken,
  )
}

export function deleteResource(
  resourceId: string,
  accessToken: string,
): Promise<void> {
  return apiFetch<void>(
    `/api/v1/admin/resources/${resourceId}`,
    { method: 'DELETE' },
    accessToken,
  )
}

export function createSlot(
  resourceId: string,
  startUtc: string,
  endUtc: string,
  accessToken: string,
): Promise<TimeSlotDto> {
  return apiFetch<TimeSlotDto>(
    `/api/v1/admin/resources/${resourceId}/slots`,
    { method: 'POST', body: JSON.stringify({ startUtc, endUtc }) },
    accessToken,
  )
}

export function deleteSlot(slotId: string, accessToken: string): Promise<void> {
  return apiFetch<void>(
    `/api/v1/admin/slots/${slotId}`,
    { method: 'DELETE' },
    accessToken,
  )
}
