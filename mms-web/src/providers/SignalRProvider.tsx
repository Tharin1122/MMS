import { createContext, useContext, useEffect, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import { useAuthStore } from '../store/authStore'
// import { CleaningCheckData } from '../types/signalr'

export interface CleaningCheckData {
  roomId: string
  roomName: string
  cleaningBufferMins: number
  askedAt: string
}
interface SignalRContextValue {
  connection: signalR.HubConnection | null
  cleaningCheckQueue: CleaningCheckData[]
  dismissCleaningCheck: (roomId: string) => void
}

const SignalRContext = createContext<SignalRContextValue>({
  connection: null,
  cleaningCheckQueue: [],
  dismissCleaningCheck: () => {},
})

export function SignalRProvider({ children }: { children: React.ReactNode }) {
  const { accessToken } = useAuthStore()
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null)  // ← เปลี่ยนเป็น state
  const [cleaningCheckQueue, setCleaningCheckQueue] = useState<CleaningCheckData[]>([])

  useEffect(() => {
    if (!accessToken) return

    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${import.meta.env.VITE_API_BASE ?? (import.meta.env.PROD ? 'https://mms-api-25xj.onrender.com' : 'http://localhost:5065')}/hubs/mms`, {
        accessTokenFactory: () => useAuthStore.getState().accessToken ?? '',
        transport: signalR.HttpTransportType.WebSockets,
        skipNegotiation: true,
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (ctx) => {
          const delays = [2000, 5000, 10000, 30000, 60000]
          return delays[Math.min(ctx.previousRetryCount, delays.length - 1)]
        }
      })
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    conn.on('CleaningCheck', (data: CleaningCheckData) => {
      console.log('🧹 CleaningCheck received:', data)
      setCleaningCheckQueue(prev =>
        prev.some(d => d.roomId === data.roomId) ? prev : [...prev, data]
      )
    })

    conn.on('RoomStatusChanged', (data) => {
      console.log('🚪 RoomStatusChanged received:', data)
    })

    conn.on('CleaningCheckDismissed', (data: { roomId: string }) => {
      console.log('✅ CleaningCheckDismissed received:', data)
      setCleaningCheckQueue(prev => prev.filter(d => d.roomId !== data.roomId))
    })

    conn.start()
      .then(() => {
        console.log('✅ SignalR connected')
        setConnection(conn)  // ← set หลัง connect สำเร็จ ทำให้ useSignalR รู้ว่า ready
      })
      .catch(err => {
        if (err?.message?.includes('stop()')) return
        console.warn('SignalR connect failed:', err)
      })

    return () => {
      setConnection(null)
      conn.stop()
    }
  }, [accessToken])

  const dismissCleaningCheck = (roomId: string) => {
    setCleaningCheckQueue(prev => prev.filter(d => d.roomId !== roomId))
  }

  return (
    <SignalRContext.Provider value={{
      connection,
      cleaningCheckQueue,
      dismissCleaningCheck,
    }}>
      {children}
    </SignalRContext.Provider>
  )
}

export const useSignalRContext = () => useContext(SignalRContext)