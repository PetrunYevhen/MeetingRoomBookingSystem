import { createContext } from 'react'
import type { HubConnection } from '@microsoft/signalr'

/** `null` until the hub connection has actually started (or when unauthenticated). */
export const RealtimeContext = createContext<HubConnection | null>(null)
