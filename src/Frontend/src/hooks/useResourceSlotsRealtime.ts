import { useContext, useEffect } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { getResourceSlots, type TimeSlotDto } from '../api/resourcesApi'
import { useAuth } from '../auth/useAuth'
import { RealtimeContext } from '../realtime/RealtimeContext'

interface SlotBookingChangedEvent {
  resourceId: string
  slotId: string
  status: 'available' | 'booked'
  occurredAtUtc: string
}

export function slotsQueryKey(resourceId: string) {
  return ['resource-slots', resourceId] as const
}

/**
 * REST is the source of truth for the initial load; SignalR only patches what changed
 * afterward (ADR 0001 — "SignalR is a freshness channel, not the source of truth").
 * Subscribes on mount, unsubscribes on unmount — the "correctly unsubscribe when
 * navigating to another page" requirement — without tearing down the shared connection.
 */
export function useResourceSlotsRealtime(resourceId: string) {
  const { accessToken } = useAuth()
  const connection = useContext(RealtimeContext)
  const queryClient = useQueryClient()

  const query = useQuery({
    queryKey: slotsQueryKey(resourceId),
    queryFn: () => getResourceSlots(resourceId, accessToken as string),
    enabled: Boolean(accessToken),
  })

  useEffect(() => {
    if (!connection) {
      return
    }

    const handleSlotBookingChanged = (event: SlotBookingChangedEvent) => {
      if (event.resourceId !== resourceId) {
        return
      }

      queryClient.setQueryData<TimeSlotDto[]>(
        slotsQueryKey(resourceId),
        (slots) =>
          slots?.map((slot) =>
            slot.id === event.slotId ? { ...slot, status: event.status } : slot,
          ),
      )
    }

    connection.on('SlotBookingChanged', handleSlotBookingChanged)
    connection.invoke('WatchResource', resourceId).catch((error: unknown) => {
      console.error('Failed to watch resource.', error)
    })

    return () => {
      connection.off('SlotBookingChanged', handleSlotBookingChanged)
      connection.invoke('UnwatchResource', resourceId).catch(() => undefined)
    }
  }, [connection, resourceId, queryClient])

  return query
}
