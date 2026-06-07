import { useState, useEffect, useCallback } from 'react'
import { api } from '../api/client'
import { useSignalR } from '../hooks/useSignalR'
import { useAuthStore } from '../store/authStore'

// ── Types ──────────────────────────────────────────
interface QueueItem {
  id: string
  queueNo: string
  arrivalTime: string
  estimatedWaitMins: number | null
  customer: { displayName: string; phone: string | null }
  services: string[]
}

interface InServiceItem {
  id: string
  queueNo: string
  startTime: string | null
  endTime: string | null
  customer: { displayName: string }
  therapists: { displayName: string; code: string | null }[]
}

interface TherapistStatus {
  id: string
  displayName: string
  code: string | null
  avatarUrl: string | null
  currentStatus: number // 0=Available, 1=Occupied, 2=Break, 3=Leave
}

interface QueueSnapshot {
  date: string
  summary: {
    waitingCount: number
    inServiceCount: number
    availableTherapists: number
    totalTherapists: number
  }
  waiting: QueueItem[]
  inService: InServiceItem[]
  therapists: TherapistStatus[]
}

const STATUS_LABEL: Record<number, string> = {
  0: 'ว่าง', 1: 'กำลังบริการ', 2: 'พัก', 3: 'ลา', 4: 'นอกเวลา', 5: 'ออฟไลน์'
}

const STATUS_COLOR: Record<number, string> = {
  0: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400',
  1: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400',
  2: 'bg-yellow-100 text-yellow-700 dark:bg-yellow-900/30 dark:text-yellow-400',
  3: 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400',
  4: 'bg-gray-100 text-gray-500',
  5: 'bg-gray-100 text-gray-400',
}

// ── Main Component ─────────────────────────────────
export default function QueueMonitorPage() {
  const [data, setData] = useState<QueueSnapshot | null>(null)
  const [loading, setLoading] = useState(true)
  const [actionLoading, setActionLoading] = useState<string | null>(null)
  const [error, setError] = useState('')
  const [activeTab, setActiveTab] = useState<'queue' | 'inservice' | 'therapist'>('queue')
  const { branchId } = useAuthStore()

  const load = useCallback(async () => {
    try {
      const res = await api.get('/queue')
      setData(res.data)
    } catch {
      setError('โหลดข้อมูลไม่สำเร็จ')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  // SignalR — realtime update
  useSignalR({
    onQueueUpdated: () => load(),
    onTherapistStatusChanged: () => load(),
    onBookingUpdated: () => load(),
  })

  // ── Actions ──────────────────────────────────────
  const startService = async (walkInId: string) => {
    setActionLoading(walkInId + '-start')
    try {
      await api.patch(`/walk-in/${walkInId}/start`)
      await load()
    } catch (e: any) {
      setError(e.response?.data?.message ?? 'เกิดข้อผิดพลาด')
    } finally {
      setActionLoading(null)
    }
  }

  const completeService = async (walkInId: string) => {
    setActionLoading(walkInId + '-complete')
    try {
      await api.patch(`/walk-in/${walkInId}/complete`)
      await load()
    } catch (e: any) {
      setError(e.response?.data?.message ?? 'เกิดข้อผิดพลาด')
    } finally {
      setActionLoading(null)
    }
  }

  const cancelWalkIn = async (walkInId: string) => {
    if (!confirm('ยืนยันยกเลิกคิวนี้?')) return
    setActionLoading(walkInId + '-cancel')
    try {
      await api.patch(`/walk-in/${walkInId}/cancel`, { reason: 'ยกเลิกโดยพนักงาน' })
      await load()
    } catch (e: any) {
      setError(e.response?.data?.message ?? 'เกิดข้อผิดพลาด')
    } finally {
      setActionLoading(null)
    }
  }

  const changeTherapistStatus = async (therapistId: string, status: number) => {
    setActionLoading(therapistId + '-status')
    try {
      await api.patch(`/therapist/${therapistId}/status`, { status, reason: null })
      await load()
    } catch (e: any) {
      setError(e.response?.data?.message ?? 'เกิดข้อผิดพลาด')
    } finally {
      setActionLoading(null)
    }
  }

  // ── Render ──────────────────────────────────────
  if (loading) return (
    <div className="flex items-center justify-center h-64">
      <div className="text-gray-400 text-sm animate-pulse">กำลังโหลด...</div>
    </div>
  )

  return (
    <div className="max-w-lg mx-auto px-4 py-4">

      {error && (
        <div className="mb-3 p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-xl text-red-600 dark:text-red-400 text-sm flex justify-between">
          <span>{error}</span>
          <button onClick={() => setError('')} className="text-red-400 hover:text-red-600">✕</button>
        </div>
      )}

      {/* Summary Cards */}
      {data && (
        <div className="grid grid-cols-4 gap-2 mb-4">
          <SummaryCard label="รอคิว" value={data.summary.waitingCount} color="orange" />
          <SummaryCard label="กำลังบริการ" value={data.summary.inServiceCount} color="blue" />
          <SummaryCard label="ช่างว่าง" value={data.summary.availableTherapists} color="emerald" />
          <SummaryCard label="ช่างทั้งหมด" value={data.summary.totalTherapists} color="gray" />
        </div>
      )}

      {/* Tabs */}
      <div className="flex bg-gray-100 dark:bg-gray-800 rounded-xl p-1 mb-4">
        {([
          { key: 'queue', label: `🎫 รอ (${data?.summary.waitingCount ?? 0})` },
          { key: 'inservice', label: `💆 บริการ (${data?.summary.inServiceCount ?? 0})` },
          { key: 'therapist', label: `👤 ช่าง` },
        ] as { key: typeof activeTab; label: string }[]).map(tab => (
          <button
            key={tab.key}
            onClick={() => setActiveTab(tab.key)}
            className={`flex-1 py-1.5 text-xs font-semibold rounded-lg transition
              ${activeTab === tab.key
                ? 'bg-white dark:bg-gray-700 text-gray-800 dark:text-white shadow-sm'
                : 'text-gray-500 hover:text-gray-700 dark:hover:text-gray-300'}`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Tab: Waiting Queue */}
      {activeTab === 'queue' && (
        <div className="space-y-3">
          {data?.waiting.length === 0 && (
            <div className="text-center py-12 text-gray-400 text-sm">ไม่มีลูกค้ารอคิว</div>
          )}
          {data?.waiting.map((item, idx) => (
            <div key={item.id} className="bg-white dark:bg-gray-800 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-700 p-4">
              <div className="flex items-start justify-between mb-3">
                <div className="flex items-center gap-3">
                  <div className={`w-10 h-10 rounded-xl flex items-center justify-center font-black text-lg
                    ${idx === 0 ? 'bg-orange-500 text-white' : 'bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-300'}`}>
                    {item.queueNo.replace('Q', '')}
                  </div>
                  <div>
                    <p className="font-semibold text-gray-800 dark:text-white text-sm">{item.customer.displayName}</p>
                    {item.customer.phone && <p className="text-xs text-gray-400">{item.customer.phone}</p>}
                    <p className="text-xs text-gray-400 mt-0.5">{item.services.join(', ')}</p>
                  </div>
                </div>
                {item.estimatedWaitMins != null && item.estimatedWaitMins > 0 && (
                  <span className="text-xs bg-orange-100 text-orange-600 dark:bg-orange-900/30 dark:text-orange-400 px-2 py-1 rounded-lg">
                    ~{item.estimatedWaitMins} นาที
                  </span>
                )}
              </div>
              <div className="flex gap-2">
                <button
                  onClick={() => startService(item.id)}
                  disabled={actionLoading === item.id + '-start'}
                  className="flex-1 py-2 bg-emerald-500 hover:bg-emerald-600 text-white text-xs font-semibold rounded-xl transition disabled:opacity-50"
                >
                  {actionLoading === item.id + '-start' ? '...' : '▶ เริ่มบริการ'}
                </button>
                <button
                  onClick={() => cancelWalkIn(item.id)}
                  disabled={actionLoading === item.id + '-cancel'}
                  className="py-2 px-3 bg-red-50 dark:bg-red-900/20 hover:bg-red-100 text-red-500 dark:text-red-400 text-xs font-semibold rounded-xl transition disabled:opacity-50"
                >
                  ยกเลิก
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Tab: In Service */}
      {activeTab === 'inservice' && (
        <div className="space-y-3">
          {data?.inService.length === 0 && (
            <div className="text-center py-12 text-gray-400 text-sm">ไม่มีลูกค้าที่กำลังรับบริการ</div>
          )}
          {data?.inService.map(item => (
            <div key={item.id} className="bg-white dark:bg-gray-800 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-700 p-4">
              <div className="flex items-center justify-between mb-3">
                <div className="flex items-center gap-3">
                  <div className="w-10 h-10 rounded-xl bg-blue-500 flex items-center justify-center font-black text-lg text-white">
                    {item.queueNo.replace('Q', '')}
                  </div>
                  <div>
                    <p className="font-semibold text-gray-800 dark:text-white text-sm">{item.customer.displayName}</p>
                    <p className="text-xs text-gray-400">
                      ช่าง: {item.therapists.map(t => t.displayName).join(', ') || '-'}
                    </p>
                    {item.endTime && (
                      <p className="text-xs text-blue-500 mt-0.5">
                        คาดว่าจบ {new Date(item.endTime).toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit' })} น.
                      </p>
                    )}
                  </div>
                </div>
              </div>
              <button
                onClick={() => completeService(item.id)}
                disabled={actionLoading === item.id + '-complete'}
                className="w-full py-2 bg-blue-500 hover:bg-blue-600 text-white text-xs font-semibold rounded-xl transition disabled:opacity-50"
              >
                {actionLoading === item.id + '-complete' ? '...' : '✓ เสร็จสิ้น'}
              </button>
            </div>
          ))}
        </div>
      )}

      {/* Tab: Therapists */}
      {activeTab === 'therapist' && (
        <div className="space-y-2">
          {data?.therapists.map(therapist => (
            <div key={therapist.id} className="bg-white dark:bg-gray-800 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-700 p-4 flex items-center gap-3">
              <div className="w-10 h-10 rounded-full bg-gray-200 dark:bg-gray-600 flex items-center justify-center text-lg flex-shrink-0">
                {therapist.avatarUrl
                  ? <img src={therapist.avatarUrl} className="w-full h-full rounded-full object-cover" alt="" />
                  : '👤'}
              </div>
              <div className="flex-1 min-w-0">
                <p className="font-semibold text-gray-800 dark:text-white text-sm truncate">{therapist.displayName}</p>
                {therapist.code && <p className="text-xs text-gray-400">{therapist.code}</p>}
              </div>
              <div className="flex items-center gap-2">
                <span className={`text-xs px-2 py-1 rounded-lg font-medium ${STATUS_COLOR[therapist.currentStatus]}`}>
                  {STATUS_LABEL[therapist.currentStatus]}
                </span>
                {/* Quick toggle Break */}
                {therapist.currentStatus === 0 && (
                  <button
                    onClick={() => changeTherapistStatus(therapist.id, 2)}
                    disabled={actionLoading === therapist.id + '-status'}
                    className="text-xs px-2 py-1 bg-yellow-50 dark:bg-yellow-900/20 text-yellow-600 dark:text-yellow-400 rounded-lg hover:bg-yellow-100 transition disabled:opacity-50"
                  >
                    พัก
                  </button>
                )}
                {therapist.currentStatus === 2 && (
                  <button
                    onClick={() => changeTherapistStatus(therapist.id, 0)}
                    disabled={actionLoading === therapist.id + '-status'}
                    className="text-xs px-2 py-1 bg-emerald-50 dark:bg-emerald-900/20 text-emerald-600 dark:text-emerald-400 rounded-lg hover:bg-emerald-100 transition disabled:opacity-50"
                  >
                    กลับ
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Refresh button */}
      <button
        onClick={load}
        className="fixed bottom-24 right-4 w-12 h-12 bg-emerald-500 hover:bg-emerald-600 text-white rounded-full shadow-lg flex items-center justify-center text-lg transition"
      >
        🔄
      </button>
    </div>
  )
}

function SummaryCard({ label, value, color }: { label: string; value: number; color: string }) {
  const colors: Record<string, string> = {
    orange: 'bg-orange-50 dark:bg-orange-900/20 text-orange-600 dark:text-orange-400',
    blue: 'bg-blue-50 dark:bg-blue-900/20 text-blue-600 dark:text-blue-400',
    emerald: 'bg-emerald-50 dark:bg-emerald-900/20 text-emerald-600 dark:text-emerald-400',
    gray: 'bg-gray-50 dark:bg-gray-700 text-gray-600 dark:text-gray-300',
  }
  return (
    <div className={`rounded-xl p-2 text-center ${colors[color]}`}>
      <p className="text-xl font-black">{value}</p>
      <p className="text-xs leading-tight mt-0.5 opacity-80">{label}</p>
    </div>
  )
}
