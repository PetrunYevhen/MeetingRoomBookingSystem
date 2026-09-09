import { useEffect, useRef, useState, type PropsWithChildren } from 'react'
import { HubConnectionBuilder, type HubConnection } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { API_BASE_URL } from '../api/httpClient'
import { useAuth } from '../auth/useAuth'
import { RealtimeContext } from './RealtimeContext'

/**
 * One hub connection for the whole authenticated session (created on login, stopped on
 * logout) — pages don't own connections, only group membership (see
 * `useResourceSlotsRealtime`). Reconnect handling lives here, once, for every resource
 * schedule query at once, rather than per page: `HubConnection.onreconnected` has no
 * matching "off" in the SignalR client, so registering it per-page-visit would leak a
 * handler on every navigation instead of cleanly unsubscribing.
 */
export function RealtimeProvider({ children }: PropsWithChildren) {
  const { user, accessToken } = useAuth()
  const tokenRef = useRef(accessToken)
  const queryClient = useQueryClient()
  const [connection, setConnection] = useState<HubConnection | null>(null)

  useEffect(() => {
    tokenRef.current = accessToken
  }, [accessToken])

  useEffect(() => {
    if (!user) {
      return
    }

    const hubConnection = new HubConnectionBuilder()
      .withUrl(new URL('/hubs/bookings', API_BASE_URL).toString(), {
        accessTokenFactory: () => tokenRef.current ?? '',
      })
      .withAutomaticReconnect()
      .build()

    // ADR 0001: "On reconnect... the SPA refetches the schedule through REST." Broad
    // invalidation by key prefix covers every open resource schedule at once.
    hubConnection.onreconnected(() => {
      void queryClient.invalidateQueries({ queryKey: ['resource-slots'] })
    })

    let cancelled = false
    hubConnection
      .start()
      .then(() => {
        if (!cancelled) setConnection(hubConnection)
      })
      .catch((error: unknown) => {
        console.error('Failed to start the realtime connection.', error)
      })

    return () => {
      cancelled = true
      setConnection(null)
      void hubConnection.stop()
    }
  }, [user, queryClient])

  return (
    <RealtimeContext.Provider value={connection}>
      {children}
    </RealtimeContext.Provider>
  )
}
