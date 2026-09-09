import { apiFetch } from './httpClient'

export interface ResourceDto {
  id: string
  name: string
}

export interface TimeSlotDto {
  id: string
  startUtc: string
  endUtc: string
  status: 'available' | 'booked'
}

export interface BookingDto {
  id: string
  timeSlotId: string
  createdAtUtc: string
}

export function listResources(accessToken: string): Promise<ResourceDto[]> {
  return apiFetch<ResourceDto[]>('/api/v1/resources', {}, accessToken)
}

export function getResource(
  resourceId: string,
  accessToken: string,
): Promise<ResourceDto> {
  return apiFetch<ResourceDto>(
    `/api/v1/resources/${resourceId}`,
    {},
    accessToken,
  )
}

export function getResourceSlots(
  resourceId: string,
  accessToken: string,
): Promise<TimeSlotDto[]> {
  return apiFetch<TimeSlotDto[]>(
    `/api/v1/resources/${resourceId}/slots`,
    {},
    accessToken,
  )
}

export function createBooking(
  timeSlotId: string,
  accessToken: string,
): Promise<BookingDto> {
  return apiFetch<BookingDto>(
    '/api/v1/bookings',
    { method: 'POST', body: JSON.stringify({ timeSlotId }) },
    accessToken,
  )
}
