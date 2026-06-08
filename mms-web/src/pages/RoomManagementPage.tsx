import { useState, useEffect, useCallback } from 'react'
import { api } from '../api/client'
import { useSignalR } from '../hooks/useSignalR'

// ── Types ──────────────────────────────────────────
interface Room {
  id: string
  name: string
  roomType: number
  capacity: number
  cleaningBufferMins: number
  currentStatus: number
  isActive: boolean
}

const ROOM_TYPE_LABEL: Record<number, string> = {
  0: 'ทั่วไป', 1: 'VIP', 2: 'ส่วนตัว', 3: 'คู่'
}

const STATUS_LABEL: Record<number, string> = {
  0: 'ว่าง', 1: 'ใช้งาน', 2: 'ทำความสะอาด', 3: 'ซ่อมบำรุง', 4: 'ปิด'
}

const STATUS_COLOR: Record<number, string> = {
  0: 'bg-emerald-500',
  1: 'bg-blue-500',
  2: 'bg-yellow-400',
  3: 'bg-red-400',
  4: 'bg-gray-400',
}

const STATUS_BG: Record<number, string> = {
  0: 'bg-emerald-50 dark:bg-emerald-900/20 border-emerald-200 dark:border-emerald-800',
  1: 'bg-blue-50 dark:bg-blue-900/20 border-blue-200 dark:border-blue-800',
  2: 'bg-yellow-50 dark:bg-yellow-900/20 border-yellow-200 dark:border-yellow-800',
  3: 'bg-red-50 dark:bg-red-900/20 border-red-200 dark:border-red-800',
  4: 'bg-gray-50 dark:bg-gray-800 border-gray-200 dark:border-gray-700',
}

type Tab = 'dashboard' | 'manage'

// ── Main ───────────────────────────────────────────
export default function RoomManagementPage() {
  const [rooms, setRooms] = useState<Room[]>([])
  const [loading, setLoading] = useState(true)
  const [tab, setTab] = useState<Tab>('dashboard')
  const [editRoom, setEditRoom] = useState<Room | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [actionLoading, setActionLoading] = useState<string | null>(null)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    try {
      const res = await api.get('/room')
      setRooms(res.data)
    } catch {
      setError('โหลดข้อมูลห้องไม่สำเร็จ')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  // SignalR — realtime room status
  useSignalR({
    onRoomStatusChanged: () => load(),
  })

  const changeStatus = async (roomId: string, status: number) => {
    setActionLoading(roomId + '-' + status)
    try {
      await api.patch(`/room/${roomId}/status`, { status, reason: null })
      await load()
    } catch (e: any) {
      setError(e.response?.data?.message ?? 'เปลี่ยน status ไม่สำเร็จ')
    } finally {
      setActionLoading(null)
    }
  }

  const deleteRoom = async (roomId: string) => {
    if (!confirm('ยืนยันลบห้องนี้?')) return
    try {
      await api.delete(`/room/${roomId}`)
      await load()
    } catch (e: any) {
      setError(e.response?.data?.message ?? 'ลบห้องไม่สำเร็จ')
    }
  }

  // summary counts
  const counts = {
    available: rooms.filter(r => r.currentStatus === 0 && r.isActive).length,
    occupied: rooms.filter(r => r.currentStatus === 1).length,
    cleaning: rooms.filter(r => r.currentStatus === 2).length,
    other: rooms.filter(r => r.currentStatus >= 3).length,
    total: rooms.filter(r => r.isActive).length,
  }

  if (loading) return (
    <div className="flex items-center justify-center h-64">
      <p className="text-gray-400 text-sm animate-pulse">กำลังโหลด...</p>
    </div>
  )

  return (
    <div className="max-w-2xl mx-auto px-4 py-4">

      {error && (
        <div className="mb-3 p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-xl text-red-600 dark:text-red-400 text-sm flex justify-between">
          <span>{error}</span>
          <button onClick={() => setError('')}>✕</button>
        </div>
      )}

      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-lg font-bold text-gray-800 dark:text-white">🚪 ห้องนวด</h1>
        {tab === 'manage' && (
          <button
            onClick={() => { setEditRoom(null); setShowForm(true) }}
            className="py-2 px-4 bg-emerald-500 hover:bg-emerald-600 text-white text-sm font-semibold rounded-xl transition"
          >
            + เพิ่มห้อง
          </button>
        )}
      </div>

      {/* Tabs */}
      <div className="flex bg-gray-100 dark:bg-gray-800 rounded-xl p-1 mb-4">
        <button
          onClick={() => setTab('dashboard')}
          className={`flex-1 py-1.5 text-xs font-semibold rounded-lg transition
            ${tab === 'dashboard' ? 'bg-white dark:bg-gray-700 text-gray-800 dark:text-white shadow-sm' : 'text-gray-500'}`}
        >
          📊 สถานะ realtime
        </button>
        <button
          onClick={() => setTab('manage')}
          className={`flex-1 py-1.5 text-xs font-semibold rounded-lg transition
            ${tab === 'manage' ? 'bg-white dark:bg-gray-700 text-gray-800 dark:text-white shadow-sm' : 'text-gray-500'}`}
        >
          ⚙️ จัดการห้อง
        </button>
      </div>

      {/* ── Tab: Dashboard ── */}
      {tab === 'dashboard' && (
        <>
          {/* Summary */}
          <div className="grid grid-cols-4 gap-2 mb-4">
            <SummaryCard label="ว่าง" value={counts.available} color="emerald" />
            <SummaryCard label="ใช้งาน" value={counts.occupied} color="blue" />
            <SummaryCard label="ทำความสะอาด" value={counts.cleaning} color="yellow" />
            <SummaryCard label="อื่นๆ" value={counts.other} color="gray" />
          </div>

          {/* Room Grid */}
          <div className="grid grid-cols-2 gap-3">
            {rooms.filter(r => r.isActive).map(room => (
              <div
                key={room.id}
                className={`rounded-2xl border p-4 shadow-sm ${STATUS_BG[room.currentStatus]}`}
              >
                {/* Header */}
                <div className="flex items-center justify-between mb-3">
                  <div>
                    <p className="font-bold text-gray-800 dark:text-white">{room.name}</p>
                    <p className="text-xs text-gray-400">{ROOM_TYPE_LABEL[room.roomType]}</p>
                  </div>
                  <span className={`text-white text-xs px-2 py-1 rounded-lg font-semibold ${STATUS_COLOR[room.currentStatus]}`}>
                    {STATUS_LABEL[room.currentStatus]}
                  </span>
                </div>

                {/* Quick action buttons */}
                <div className="space-y-1.5">
                  {room.currentStatus !== 0 && (
                    <button
                      onClick={() => changeStatus(room.id, 0)}
                      disabled={actionLoading?.startsWith(room.id)}
                      className="w-full py-1.5 bg-emerald-500 hover:bg-emerald-600 text-white text-xs font-semibold rounded-lg transition disabled:opacity-50"
                    >
                      ✓ ว่าง
                    </button>
                  )}
                  {room.currentStatus !== 1 && (
                    <button
                      onClick={() => changeStatus(room.id, 1)}
                      disabled={actionLoading?.startsWith(room.id)}
                      className="w-full py-1.5 bg-blue-500 hover:bg-blue-600 text-white text-xs font-semibold rounded-lg transition disabled:opacity-50"
                    >
                      ▶ ใช้งาน
                    </button>
                  )}
                  {room.currentStatus !== 2 && (
                    <button
                      onClick={() => changeStatus(room.id, 2)}
                      disabled={actionLoading?.startsWith(room.id)}
                      className="w-full py-1.5 bg-yellow-400 hover:bg-yellow-500 text-white text-xs font-semibold rounded-lg transition disabled:opacity-50"
                    >
                      🧹 ทำความสะอาด
                    </button>
                  )}
                  {room.currentStatus !== 3 && (
                    <button
                      onClick={() => changeStatus(room.id, 3)}
                      disabled={actionLoading?.startsWith(room.id)}
                      className="w-full py-1.5 bg-red-400 hover:bg-red-500 text-white text-xs font-semibold rounded-lg transition disabled:opacity-50"
                    >
                      🔧 ซ่อมบำรุง
                    </button>
                  )}
                </div>

                {/* Buffer info */}
                {room.currentStatus === 2 && (
                  <p className="text-xs text-yellow-600 dark:text-yellow-400 mt-2 text-center">
                    ⏱ buffer {room.cleaningBufferMins} นาที
                  </p>
                )}
              </div>
            ))}
          </div>

          {rooms.filter(r => r.isActive).length === 0 && (
            <div className="text-center py-12 text-gray-400 text-sm">
              ยังไม่มีห้อง — ไปที่แท็บ "จัดการห้อง" เพื่อเพิ่ม
            </div>
          )}

          {/* Refresh */}
          <button
            onClick={load}
            className="fixed bottom-24 right-4 w-12 h-12 bg-emerald-500 hover:bg-emerald-600 text-white rounded-full shadow-lg flex items-center justify-center text-lg transition"
          >
            🔄
          </button>
        </>
      )}

      {/* ── Tab: Manage ── */}
      {tab === 'manage' && (
        <div className="space-y-3">
          {rooms.length === 0 && (
            <div className="text-center py-12 text-gray-400 text-sm">ยังไม่มีห้อง กด "+ เพิ่มห้อง"</div>
          )}
          {rooms.map(room => (
            <div key={room.id} className="bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 p-4 shadow-sm">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className="text-2xl">🚪</div>
                  <div>
                    <p className="font-semibold text-gray-800 dark:text-white">{room.name}</p>
                    <p className="text-xs text-gray-400">
                      {ROOM_TYPE_LABEL[room.roomType]} · {room.capacity} คน · buffer {room.cleaningBufferMins} นาที
                    </p>
                    {!room.isActive && (
                      <span className="text-xs text-red-400">ปิดใช้งาน</span>
                    )}
                  </div>
                </div>
                <div className="flex gap-2">
                  <button
                    onClick={() => { setEditRoom(room); setShowForm(true) }}
                    className="py-1.5 px-3 bg-gray-100 dark:bg-gray-700 hover:bg-gray-200 dark:hover:bg-gray-600 text-gray-700 dark:text-gray-300 text-xs font-semibold rounded-lg transition"
                  >
                    แก้ไข
                  </button>
                  <button
                    onClick={() => deleteRoom(room.id)}
                    className="py-1.5 px-3 bg-red-50 dark:bg-red-900/20 hover:bg-red-100 text-red-500 dark:text-red-400 text-xs font-semibold rounded-lg transition"
                  >
                    ลบ
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* ── Modal Form ── */}
      {showForm && (
        <RoomFormModal
          room={editRoom}
          onClose={() => { setShowForm(false); setEditRoom(null) }}
          onSaved={() => { setShowForm(false); setEditRoom(null); load() }}
        />
      )}
    </div>
  )
}

// ── Room Form Modal ─────────────────────────────────
function RoomFormModal({
  room, onClose, onSaved
}: {
  room: Room | null
  onClose: () => void
  onSaved: () => void
}) {
  const isEdit = !!room
  const [form, setForm] = useState({
    name: room?.name ?? '',
    roomType: room?.roomType ?? 0,
    capacity: room?.capacity ?? 1,
    cleaningBufferMins: room?.cleaningBufferMins ?? 10,
    isActive: room?.isActive ?? true,
  })
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const set = (key: string, value: any) =>
    setForm(prev => ({ ...prev, [key]: value }))

  const save = async () => {
    if (!form.name.trim()) { setError('กรุณาใส่ชื่อห้อง'); return }
    setLoading(true)
    setError('')
    try {
      if (isEdit) {
        await api.put(`/room/${room!.id}`, form)
      } else {
        await api.post('/room', form)
      }
      onSaved()
    } catch (e: any) {
      setError(e.response?.data?.message ?? 'บันทึกไม่สำเร็จ')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-end sm:items-center justify-center bg-black/50 backdrop-blur-sm px-4 pb-4">
      <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-2xl p-6 w-full max-w-sm">
        <h2 className="text-lg font-bold text-gray-800 dark:text-white mb-4">
          {isEdit ? '✏️ แก้ไขห้อง' : '➕ เพิ่มห้องใหม่'}
        </h2>

        {error && (
          <div className="mb-3 p-2 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg text-red-600 dark:text-red-400 text-xs">
            {error}
          </div>
        )}

        <div className="space-y-3">
          {/* ชื่อห้อง */}
          <div>
            <label className={labelClass}>ชื่อห้อง</label>
            <input
              type="text"
              value={form.name}
              onChange={e => set('name', e.target.value)}
              placeholder="เช่น ห้อง A1, VIP 01"
              className={inputClass}
            />
          </div>

          {/* ประเภทห้อง */}
          <div>
            <label className={labelClass}>ประเภทห้อง</label>
            <select
              value={form.roomType}
              onChange={e => set('roomType', Number(e.target.value))}
              className={inputClass}
            >
              {Object.entries(ROOM_TYPE_LABEL).map(([v, l]) => (
                <option key={v} value={v}>{l}</option>
              ))}
            </select>
          </div>

          {/* ความจุ */}
          <div>
            <label className={labelClass}>ความจุ (คน)</label>
            <input
              type="number"
              min={1} max={10}
              value={form.capacity}
              onChange={e => set('capacity', Number(e.target.value))}
              className={inputClass}
            />
          </div>

          {/* Cleaning Buffer */}
          <div>
            <label className={labelClass}>เวลาทำความสะอาด (นาที)</label>
            <input
              type="number"
              min={1} max={60}
              value={form.cleaningBufferMins}
              onChange={e => set('cleaningBufferMins', Number(e.target.value))}
              className={inputClass}
            />
            <p className="text-xs text-gray-400 mt-1">
              หลังเสร็จ backend จะถามว่าห้องพร้อมหรือยังหลังจาก {form.cleaningBufferMins} นาที
            </p>
          </div>

          {/* Active */}
          <div className="flex items-center gap-3">
            <label className="relative inline-flex items-center cursor-pointer">
              <input
                type="checkbox"
                checked={form.isActive}
                onChange={e => set('isActive', e.target.checked)}
                className="sr-only peer"
              />
              <div className="w-10 h-6 bg-gray-200 peer-focus:outline-none rounded-full peer dark:bg-gray-700 peer-checked:after:translate-x-full peer-checked:bg-emerald-500 after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:rounded-full after:h-5 after:w-5 after:transition-all" />
            </label>
            <span className="text-sm text-gray-700 dark:text-gray-300">เปิดใช้งาน</span>
          </div>
        </div>

        {/* Buttons */}
        <div className="flex gap-3 mt-5">
          <button
            onClick={onClose}
            className="flex-1 py-2.5 bg-gray-100 dark:bg-gray-700 hover:bg-gray-200 dark:hover:bg-gray-600 text-gray-700 dark:text-gray-300 font-semibold rounded-xl text-sm transition"
          >
            ยกเลิก
          </button>
          <button
            onClick={save}
            disabled={loading}
            className="flex-1 py-2.5 bg-emerald-500 hover:bg-emerald-600 text-white font-semibold rounded-xl text-sm transition disabled:opacity-50"
          >
            {loading ? 'กำลังบันทึก...' : isEdit ? 'บันทึก' : 'เพิ่มห้อง'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Summary Card ────────────────────────────────────
function SummaryCard({ label, value, color }: { label: string; value: number; color: string }) {
  const colors: Record<string, string> = {
    emerald: 'bg-emerald-50 dark:bg-emerald-900/20 text-emerald-600 dark:text-emerald-400',
    blue: 'bg-blue-50 dark:bg-blue-900/20 text-blue-600 dark:text-blue-400',
    yellow: 'bg-yellow-50 dark:bg-yellow-900/20 text-yellow-600 dark:text-yellow-400',
    gray: 'bg-gray-50 dark:bg-gray-700 text-gray-600 dark:text-gray-300',
  }
  return (
    <div className={`rounded-xl p-2 text-center ${colors[color]}`}>
      <p className="text-xl font-black">{value}</p>
      <p className="text-xs leading-tight mt-0.5 opacity-80">{label}</p>
    </div>
  )
}

const inputClass = "w-full border border-gray-300 dark:border-gray-600 rounded-xl px-3 py-2 text-sm bg-white dark:bg-gray-700 text-gray-800 dark:text-white focus:outline-none focus:border-emerald-400"
const labelClass = "block text-xs font-semibold text-gray-600 dark:text-gray-400 mb-1"
