import { useEffect, useRef } from 'react'
import * as signalR from '@microsoft/signalr'
import { useDashboardStore } from '../store/dashboardStore'
import { useAuthStore } from '../store/authStore'
import type { DashboardSnapshot } from '../types'

export function useSignalR() {
  const connectionRef = useRef<signalR.HubConnection | null>(null)
  const { accessToken } = useAuthStore()
  const { setSnapshot } = useDashboardStore()

  useEffect(() => {
    if (!accessToken) return

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/mms', {
        accessTokenFactory: () => accessToken
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    // รับ Dashboard Snapshot
    connection.on('DashboardSnapshot', (data: DashboardSnapshot) => {
      setSnapshot(data)
    })

    // รับ Therapist Status
    connection.on('TherapistStatusChanged', (data) => {
      console.log('TherapistStatusChanged:', data)
      // store จะ update ผ่าน DashboardSnapshot broadcast
    })

    // รับ Queue
    connection.on('QueueUpdated', (data) => {
      console.log('QueueUpdated:', data)
    })

    connection.start()
      .then(() => console.log('SignalR connected'))
      .catch((err) => console.error('SignalR error:', err))

    connectionRef.current = connection

    return () => {
      connection.stop()
    }
  }, [accessToken])

  return connectionRef
}