export interface User {
  id: string
  displayName: string
  avatarUrl: string | null
  tenantId: string
  branchId: string
}

export interface AuthState {
  accessToken: string | null
  refreshToken: string | null
  user: User | null
  permissions: string[]
}

export interface TherapistSummaryItem {
  id: string
  displayName: string
  code: string | null
  avatarUrl: string | null
  currentStatus: number // 0=Available,1=Occupied,2=Break,3=Leave,4=OffDuty,5=Offline
}

export interface RoomSummaryItem {
  id: string
  name: string
  roomType: number
  currentStatus: number // 0=Available,1=Occupied,2=Cleaning,3=Maintenance,4=Closed
  cleaningBufferMins: number
}

export interface QueueItem {
  id: string
  queueNo: string
  status: number
  arrivalTime: string
  startTime: string | null
  endTime: string | null
  estimatedWaitMins: number | null
  customer: { displayName: string; phone: string | null }
  serviceCount: number
}

export interface DashboardSnapshot {
  date: string
  generatedAt: string
  therapists: {
    total: number
    available: number
    occupied: number
    onBreak: number
    onLeave: number
    offline: number
    list: TherapistSummaryItem[]
  }
  rooms: {
    total: number
    available: number
    occupied: number
    cleaning: number
    maintenance: number
    list: RoomSummaryItem[]
  }
  queue: {
    totalToday: number
    waiting: number
    inService: number
    completed: number
    cancelled: number
    waitingList: QueueItem[]
    inServiceList: QueueItem[]
  }
  bookings: {
    total: number
    pending: number
    confirmed: number
    inProgress: number
    completed: number
    cancelled: number
    noShow: number
  }
  revenue: {
    totalReceipts: number
    totalRevenue: number
    totalDiscount: number
  }
}