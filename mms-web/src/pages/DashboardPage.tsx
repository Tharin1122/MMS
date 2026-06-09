import { useEffect, useState, useCallback } from 'react'
import { useDashboardStore } from '../store/dashboardStore'
import { useSignalR } from '../hooks/useSignalR'
import { useSignalRContext } from '../providers/SignalRProvider'
import { api } from '../api/client'
import { DonutChart } from '../components/charts/DonutChart'
import { TherapistTimeline } from '../components/TherapistTimeline'
import { PlanGate } from '../components/plan/PlanGate'
import type { DashboardSnapshot, QueueItem } from '../types/dashboard'

// ────────────────────────────────────────────────────────────
// Sub-components
// ────────────────────────────────────────────────────────────

function StatCard({
  icon, label, value, sub, badge, trend, color,
}: {
  icon: React.ReactNode
  label: string
  value: string
  sub?: string
  badge?: string
  trend?: string
  color: string
}) {
  return (
    <div className="bg-white rounded-xl border border-gray-100 p-4 flex items-start gap-3 shadow-sm">
      <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${color}`}>
        {icon}
      </div>
      <div className="flex-1 min-w-0">
        <p className="text-xs text-gray-500 mb-0.5">{label}</p>
        <p className="text-xl font-bold text-gray-800 leading-tight">{value}</p>
        {sub && <p className="text-xs text-gray-400 mt-0.5">{sub}</p>}
      </div>
      <div className="text-right flex-shrink-0">
        {trend && (
          <span className="text-xs text-green-500 font-medium">{trend}</span>
        )}
        {badge && (
          <p className="text-xs text-orange-500 mt-1">{badge}</p>
        )}
      </div>
    </div>
  )
}

// Donut + booking status breakdown
function BookingOverview({ snap }: { snap: DashboardSnapshot }) {
  const { bookings } = snap
  const total = bookings.total

  const segments = [
    { label: 'เสร็จสิ้น',       value: bookings.completed,  color: '#22c55e' },
    { label: 'กำลังให้บริการ',  value: bookings.inProgress, color: '#3b82f6' },
    { label: 'รอดำเนินการ',     value: bookings.pending + bookings.confirmed, color: '#f97316' },
    { label: 'ยกเลิก',          value: bookings.cancelled,  color: '#e5e7eb' },
  ]

  return (
    <div className="bg-white rounded-xl border border-gray-100 p-4 shadow-sm">
      <h3 className="text-sm font-semibold text-gray-700 mb-4">ภาพรวมการจองวันนี้</h3>
      <div className="flex items-center gap-6">
        <div className="relative flex-shrink-0">
          <DonutChart segments={segments} total={total} size={140} strokeWidth={24} />
          <div className="absolute inset-0 flex flex-col items-center justify-center pointer-events-none">
            <p className="text-sm font-bold text-gray-800">{total}</p>
            <p className="text-xs text-gray-400">ทั้งหมด</p>
          </div>
        </div>
        <div className="flex-1 space-y-2">
          {segments.map(seg => (
            <div key={seg.label} className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <span className="w-3 h-3 rounded-full flex-shrink-0" style={{ background: seg.color }} />
                <span className="text-xs text-gray-600">{seg.label}</span>
              </div>
              <div className="flex items-center gap-2">
                <span className="text-xs font-semibold text-gray-800">{seg.value}</span>
                <span className="text-xs text-gray-400 w-10 text-right">
                  ({total > 0 ? Math.round((seg.value / total) * 100) : 0}%)
                </span>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}

// Revenue summary table
function RevenuePanel({ snap }: { snap: DashboardSnapshot }) {
  const { revenue, monthlyRevenue } = snap
  const profit = monthlyRevenue.totalRevenue * 0.79  // mock: ~21% expense ratio

  const methodLabel: Record<string, string> = {
    Cash: 'เงินสด', Transfer: 'โอนเงิน', QR: 'QR / พร้อมเพย์', Card: 'บัตรเครดิต',
  }

  return (
    <div className="bg-white rounded-xl border border-gray-100 p-4 shadow-sm">
      <div className="flex items-center justify-between mb-3">
        <h3 className="text-sm font-semibold text-gray-700">รายรับ – รายจ่าย (เดือนนี้)</h3>
      </div>
      <table className="w-full text-xs">
        <thead>
          <tr className="text-gray-400 border-b border-gray-100">
            <th className="text-left pb-2 font-normal">รายการ</th>
            <th className="text-right pb-2 font-normal">จำนวนเงิน (บาท)</th>
          </tr>
        </thead>
        <tbody>
          {revenue.byMethod.map(m => (
            <tr key={m.method} className="border-b border-gray-50">
              <td className="py-1.5 text-gray-600">{methodLabel[m.method] ?? m.method}</td>
              <td className="py-1.5 text-right text-gray-800">{m.amount.toLocaleString('th-TH', { minimumFractionDigits: 2 })}</td>
            </tr>
          ))}
          <tr className="border-b border-gray-100 font-medium">
            <td className="py-2 text-gray-700">รายรับรวม</td>
            <td className="py-2 text-right text-gray-900">{monthlyRevenue.totalRevenue.toLocaleString('th-TH', { minimumFractionDigits: 2 })}</td>
          </tr>
          <tr className="border-b border-gray-100">
            <td className="py-1.5 text-gray-500">รายจ่ายรวม</td>
            <td className="py-1.5 text-right text-red-500">{(monthlyRevenue.totalRevenue - profit).toLocaleString('th-TH', { minimumFractionDigits: 2 })}</td>
          </tr>
          <tr>
            <td className="pt-2 font-semibold text-gray-700">กำไรสุทธิ</td>
            <td className="pt-2 text-right font-bold text-violet-600">{profit.toLocaleString('th-TH', { minimumFractionDigits: 2 })}</td>
          </tr>
        </tbody>
      </table>
      <button className="mt-3 w-full text-xs text-violet-600 hover:text-violet-800 transition">
        ดูรายงานการเงิน →
      </button>
    </div>
  )
}

// Queue panel (right sidebar)
function QueuePanel({ snap }: { snap: DashboardSnapshot }) {
  const waiting = snap.queue.waitingList
  const inService = snap.queue.inServiceList

  const now = new Date()

  const minutesSince = (t?: string) => {
    if (!t) return null
    return Math.floor((now.getTime() - new Date(t).getTime()) / 60000)
  }

  return (
    <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">
      <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100">
        <h3 className="text-sm font-semibold text-gray-700">
          คิวรอดำเนินการ (เรียลไทม์)
          {waiting.length > 0 && (
            <span className="ml-2 bg-red-500 text-white text-xs px-1.5 py-0.5 rounded-full">{waiting.length}</span>
          )}
        </h3>
      </div>
      <div className="divide-y divide-gray-50 max-h-72 overflow-y-auto">
        {[...waiting, ...inService].length === 0 ? (
          <p className="text-sm text-gray-400 text-center py-6">ไม่มีคิวรอ</p>
        ) : (
          [...waiting, ...inService].map((q: QueueItem) => {
            const mins = minutesSince(q.arrivalTime)
            const isOverdue = mins !== null && mins > 30 && q.status === 0
            return (
              <div key={q.id} className="flex items-center gap-3 px-4 py-2.5">
                <div className="w-8 h-8 rounded-full bg-gray-100 overflow-hidden flex-shrink-0 flex items-center justify-center text-sm">
                  👤
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-xs font-medium text-gray-800 truncate">
                    {q.customer.displayName}
                  </p>
                  <p className="text-xs text-gray-400">{q.serviceCount} บริการ</p>
                </div>
                <div className="text-right">
                  <p className="text-xs font-medium text-gray-700">
                    {q.startTime
                      ? new Date(q.startTime).toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit' })
                      : new Date(q.arrivalTime).toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit' })}
                  </p>
                  {isOverdue
                    ? <p className="text-xs text-red-500">เกินเวลา {mins} น.</p>
                    : q.estimatedWaitMins
                      ? <p className="text-xs text-gray-400">~{q.estimatedWaitMins} น.</p>
                      : null}
                </div>
              </div>
            )
          })
        )}
      </div>
      <div className="px-4 py-2 border-t border-gray-100">
        <button className="w-full bg-violet-600 hover:bg-violet-700 text-white text-xs py-2 rounded-lg transition">
          จัดการคิวทั้งหมด
        </button>
      </div>
    </div>
  )
}

// Mini Calendar
function MiniCalendar({ snap: _snap }: { snap: DashboardSnapshot }) {
  const now = new Date()
  const year = now.getFullYear()
  const month = now.getMonth()
  const today = now.getDate()
  const firstDay = new Date(year, month, 1).getDay()
  const daysInMonth = new Date(year, month + 1, 0).getDate()
  const thMonths = ['ม.ค.','ก.พ.','มี.ค.','เม.ย.','พ.ค.','มิ.ย.','ก.ค.','ส.ค.','ก.ย.','ต.ค.','พ.ย.','ธ.ค.']
  const days = ['อา','จ','อ','พ','พฤ','ศ','ส']

  // days that have bookings (mock: today + a few)
  const bookedDays = new Set([today, today + 2, today + 5].filter(d => d <= daysInMonth))

  return (
    <div className="bg-white rounded-xl border border-gray-100 p-4 shadow-sm">
      <div className="flex items-center justify-between mb-3">
        <h3 className="text-sm font-semibold text-gray-700">ปฏิทินการจอง</h3>
        <span className="text-xs text-gray-400">{thMonths[month]} {year + 543}</span>
      </div>
      <div className="grid grid-cols-7 gap-0.5 text-center">
        {days.map(d => (
          <div key={d} className="text-xs text-gray-400 pb-1">{d}</div>
        ))}
        {Array.from({ length: firstDay }, (_, i) => <div key={`e${i}`} />)}
        {Array.from({ length: daysInMonth }, (_, i) => {
          const d = i + 1
          const isToday = d === today
          const hasBooking = bookedDays.has(d)
          return (
            <div
              key={d}
              className={`text-xs py-1 rounded-full cursor-pointer transition
                ${isToday ? 'bg-violet-600 text-white font-bold' : hasBooking ? 'text-violet-700 font-medium' : 'text-gray-600 hover:bg-gray-100'}`}
            >
              {d}
              {hasBooking && !isToday && (
                <span className="block w-1 h-1 rounded-full bg-violet-400 mx-auto mt-0.5" />
              )}
            </div>
          )
        })}
      </div>
      <div className="mt-3 text-xs text-gray-400 text-center">
        <span className="w-2 h-2 rounded-full bg-violet-600 inline-block mr-1" /> = วันนี้
      </div>
    </div>
  )
}

// Notification list
function NotificationPanel({ snap }: { snap: DashboardSnapshot }) {
  const notifs = [
    { color: 'bg-green-400', text: snap.queue.inServiceList[0] ? `${snap.queue.inServiceList[0].customer.displayName} เช็คอินแล้ว` : 'ระบบเริ่มต้นแล้ว', time: 'เมื่อกี้', type: 'success' },
    { color: 'bg-orange-400', text: 'ตรวจสอบคิวใหม่', time: 'เมื่อกี้', type: 'warning' },
    { color: 'bg-red-400',   text: 'สต็อกน้ำมันนวดใกล้หมด', time: 'เมื่อวาน', type: 'error' },
  ]

  return (
    <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">
      <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100">
        <h3 className="text-sm font-semibold text-gray-700">การแจ้งเตือน</h3>
        <button className="text-xs text-violet-500 hover:text-violet-700">ดูทั้งหมด</button>
      </div>
      <div className="divide-y divide-gray-50">
        {notifs.map((n, i) => (
          <div key={i} className="flex items-center gap-3 px-4 py-2.5">
            <span className={`w-2 h-2 rounded-full flex-shrink-0 ${n.color}`} />
            <p className="text-xs text-gray-700 flex-1">{n.text}</p>
            <span className="text-xs text-gray-400 flex-shrink-0">{n.time}</span>
          </div>
        ))}
      </div>
    </div>
  )
}

// ────────────────────────────────────────────────────────────
// Main Page
// ────────────────────────────────────────────────────────────

export default function DashboardPage() {
  const { snapshot, isLoading, setSnapshot, setLoading } = useDashboardStore()
  const { connection } = useSignalRContext()
  const [scheduleKey, setScheduleKey] = useState(0)

  const fetchSnapshot = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get('/dashboard')
      setSnapshot(res.data as DashboardSnapshot)
    } catch (err) {
      console.error(err)
    } finally {
      setLoading(false)
    }
  }, [setLoading, setSnapshot])

  // Listen to SignalR — no polling when connected
  useSignalR({
    onQueueUpdated:          () => { fetchSnapshot(); setScheduleKey(k => k + 1) },
    onTherapistStatusChanged: () => { fetchSnapshot(); setScheduleKey(k => k + 1) },
    onBookingUpdated:        () => { fetchSnapshot(); setScheduleKey(k => k + 1) },
    onRoomStatusChanged:     () => { fetchSnapshot(); setScheduleKey(k => k + 1) },
    onDashboardSnapshot:     (data) => setSnapshot(data),
  })

  useEffect(() => {
    fetchSnapshot()
  }, [fetchSnapshot])

  // Fallback poll ONLY when SignalR is not connected
  useEffect(() => {
    if (connection) return // connected → no polling needed
    const interval = setInterval(fetchSnapshot, 30000)
    return () => clearInterval(interval)
  }, [connection, fetchSnapshot])

  const snap = snapshot as DashboardSnapshot | null
  const planType = snap?.plan?.planType ?? 'Free'

  if (isLoading && !snap) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-center">
          <div className="w-8 h-8 border-4 border-violet-600 border-t-transparent rounded-full animate-spin mx-auto mb-2" />
          <p className="text-sm text-gray-400">กำลังโหลด...</p>
        </div>
      </div>
    )
  }

  if (!snap) return null

  const todayLabel = new Date().toLocaleDateString('th-TH', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })

  return (
    <div className="p-6 space-y-5">
      {/* Top bar */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-lg font-bold text-gray-800">แดชบอร์ด</h1>
          <p className="text-xs text-gray-400">{todayLabel}</p>
        </div>
        <div className="flex items-center gap-2">
          {connection && (
            <span className="flex items-center gap-1 text-xs text-green-500">
              <span className="w-2 h-2 rounded-full bg-green-400 animate-pulse" />
              เรียลไทม์
            </span>
          )}
          <button
            onClick={fetchSnapshot}
            className="text-xs bg-gray-100 hover:bg-gray-200 text-gray-600 px-3 py-1.5 rounded-lg transition"
          >
            ↺ รีเฟรช
          </button>
          <button className="flex items-center gap-1.5 text-xs bg-violet-600 hover:bg-violet-700 text-white px-3 py-1.5 rounded-lg transition">
            + สร้างการจอง
          </button>
        </div>
      </div>

      {/* Stat cards */}
      <div className="grid grid-cols-2 xl:grid-cols-4 gap-4">
        <StatCard
          icon={<span className="text-white text-xl">💰</span>}
          label="รายรับวันนี้"
          value={`${snap.revenue.totalRevenue.toLocaleString('th-TH')} บาท`}
          sub={`จาก ${snap.revenue.totalReceipts} การจอง`}
          trend="+12.5%"
          color="bg-violet-500"
        />
        <StatCard
          icon={<span className="text-white text-xl">📈</span>}
          label="รายรับเดือนนี้"
          value={`${snap.monthlyRevenue.totalRevenue.toLocaleString('th-TH')} บาท`}
          sub={`จาก ${snap.monthlyRevenue.totalReceipts} การจอง`}
          trend="+8.3%"
          color="bg-green-500"
        />
        <StatCard
          icon={<span className="text-white text-xl">👥</span>}
          label="ลูกค้าวันนี้"
          value={`${snap.queue.totalToday} คน`}
          sub={`ลูกค้าใหม่ ${snap.queue.waiting} คน`}
          trend="+9.1%"
          color="bg-orange-400"
        />
        <StatCard
          icon={<span className="text-white text-xl">📋</span>}
          label="การจองวันนี้"
          value={`${snap.bookings.total} การจอง`}
          sub={`รอดำเนินการ ${snap.bookings.pending + snap.bookings.confirmed}`}
          badge={snap.bookings.pending > 0 ? `รอดำเนินการ ${snap.bookings.pending}` : undefined}
          color="bg-blue-400"
        />
      </div>

      {/* Middle: Chart + Revenue */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
        <div className="lg:col-span-2">
          <BookingOverview snap={snap} />
        </div>
        <div>
          <RevenuePanel snap={snap} />
        </div>
      </div>

      {/* Timeline + Right panel */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
        <div className="lg:col-span-2">
          <TherapistTimeline refreshKey={scheduleKey} />
        </div>
        <div className="space-y-4">
          <QueuePanel snap={snap} planType={planType} />
          <MiniCalendar snap={snap} />
          <PlanGate required="Basic" currentPlan={planType} mode="blur">
            <NotificationPanel snap={snap} />
          </PlanGate>
        </div>
      </div>

      {/* Bottom quick actions */}
      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-5 gap-3">
        {[
          { icon: '📋', label: 'สร้างการจอง',   sub: 'เพิ่มการจองใหม่' },
          { icon: '👤', label: 'ลูกค้าใหม่',    sub: 'เพิ่มลูกค้า' },
          { icon: '🎫', label: 'จัดการคิว',     sub: 'คิวรอ / กำลังบริการ' },
          { icon: '💵', label: 'รายงานการเงิน', sub: 'รายรับ – รายจ่าย' },
          { icon: '⚙️', label: 'ตั้งค่าระบบ',  sub: 'ตั้งค่าทั่วไป' },
        ].map(a => (
          <button
            key={a.label}
            className="bg-white rounded-xl border border-gray-100 p-3 text-left hover:border-violet-300 hover:shadow-sm transition shadow-sm"
          >
            <div className="text-2xl mb-1">{a.icon}</div>
            <p className="text-xs font-medium text-gray-700">{a.label}</p>
            <p className="text-xs text-gray-400">{a.sub}</p>
          </button>
        ))}
      </div>
    </div>
  )
}
