import { useEffect, useRef } from 'react'
import { useSignalRContext } from '../providers/SignalRProvider'

interface UseSignalROptions {
  onQueueUpdated?: (data: any) => void
  onTherapistStatusChanged?: (data: any) => void
  onBookingUpdated?: (data: any) => void
  onDashboardSnapshot?: (data: any) => void
  onRoomStatusChanged?: (data: any) => void
}

export function useSignalR(options: UseSignalROptions = {}) {
  const optionsRef = useRef(options)  // ย้ายขึ้นมาก่อน useContext
  optionsRef.current = options

  const { connection } = useSignalRContext()

  useEffect(() => {
    if (!connection) return

    const onQueueUpdated      = (d: any) => optionsRef.current.onQueueUpdated?.(d)
    const onTherapistChanged  = (d: any) => optionsRef.current.onTherapistStatusChanged?.(d)
    const onBookingUpdated    = (d: any) => optionsRef.current.onBookingUpdated?.(d)
    const onDashboardSnapshot = (d: any) => optionsRef.current.onDashboardSnapshot?.(d)
    const onRoomStatusChanged = (d: any) => optionsRef.current.onRoomStatusChanged?.(d)

    connection.on('QueueUpdated',           onQueueUpdated)
    connection.on('TherapistStatusChanged', onTherapistChanged)
    connection.on('BookingUpdated',         onBookingUpdated)
    connection.on('DashboardSnapshot',      onDashboardSnapshot)
    connection.on('RoomStatusChanged',      onRoomStatusChanged)

    return () => {
      connection.off('QueueUpdated',           onQueueUpdated)
      connection.off('TherapistStatusChanged', onTherapistChanged)
      connection.off('BookingUpdated',         onBookingUpdated)
      connection.off('DashboardSnapshot',      onDashboardSnapshot)
      connection.off('RoomStatusChanged',      onRoomStatusChanged)
    }
  }, [connection])

  return connection
}