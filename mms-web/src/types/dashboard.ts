export interface TherapistItem {
  id: string
  displayName: string
  code: string | null
  avatarUrl?: string | null
  currentStatus: number
}

export interface RoomItem {
  id: string
  name: string
  roomType: number
  currentStatus: number
  cleaningBufferMins: number
}

export interface QueueItem {
  id: string
  queueNo: string
  status: number
  arrivalTime: string
  startTime?: string
  endTime?: string
  estimatedWaitMins?: number
  customer: { displayName: string; phone?: string }
  serviceCount: number
}

export interface BookingItem {
  id: string
  bookingNo: string
  startTime: string
  endTime: string
  totalAmount: number
  status: number
  customer: { displayName: string; phone?: string }
  itemCount: number
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
    list: TherapistItem[]
  }
  rooms: {
    total: number
    available: number
    occupied: number
    cleaning: number
    maintenance: number
    list: RoomItem[]
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
    upcomingList: BookingItem[]
  }
  revenue: {
    totalReceipts: number
    totalRevenue: number
    totalDiscount: number
    byMethod: { method: string; count: number; amount: number }[]
  }
  monthlyRevenue: {
    totalRevenue: number
    totalReceipts: number
  }
  plan: {
    planType: string
    trialEndsAt?: string
  }
}

export interface ScheduleItem {
  startTime?: string
  endTime?: string
  serviceName: string
  serviceCategory: string
  customerName: string
  source: 'walkin' | 'booking'
}

export interface TherapistSchedule {
  id: string
  displayName: string
  avatarUrl?: string
  currentStatus: number
  items: ScheduleItem[]
}

export interface DashboardSchedule {
  date: string
  therapists: TherapistSchedule[]
}
