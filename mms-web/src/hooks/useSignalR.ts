import { useEffect, useRef } from 'react'
import * as signalR from '@microsoft/signalr'
import { useAuthStore } from '../store/authStore'

interface UseSignalROptions {
  onQueueUpdated?: (data: any) => void
  onTherapistStatusChanged?: (data: any) => void
  onBookingUpdated?: (data: any) => void
  onDashboardSnapshot?: (data: any) => void
}

export function useSignalR(options: UseSignalROptions = {}) {
  const { accessToken } = useAuthStore()
  const connectionRef = useRef<signalR.HubConnection | null>(null)
  const optionsRef = useRef(options)
  optionsRef.current = options // ไม่ให้ stale closure

  useEffect(() => {
    if (!accessToken) return

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5065/hubs/mms', {
        accessTokenFactory: () => accessToken,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    connectionRef.current = connection

    connection.on('QueueUpdated', (data) => {
      optionsRef.current.onQueueUpdated?.(data)
    })

    connection.on('TherapistStatusChanged', (data) => {
      optionsRef.current.onTherapistStatusChanged?.(data)
    })

    connection.on('BookingUpdated', (data) => {
      optionsRef.current.onBookingUpdated?.(data)
    })

    connection.on('DashboardSnapshot', (data) => {
      optionsRef.current.onDashboardSnapshot?.(data)
    })

    connection.start().catch(err =>
      console.warn('SignalR connection failed:', err)
    )

    return () => {
      connection.stop()
    }
  }, [accessToken])

  return connectionRef
}
