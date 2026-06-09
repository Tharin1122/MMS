import { useEffect, useState } from 'react'
import { api } from '../api/client'
import type { DashboardSchedule, ScheduleItem } from '../types/dashboard'

const START_HOUR = 9
const END_HOUR = 22
const TOTAL_MINS = (END_HOUR - START_HOUR) * 60

const therapistStatusColor: Record<number, string> = {
  0: 'bg-green-400',  // available
  1: 'bg-blue-500',   // occupied
  2: 'bg-yellow-400', // break
  3: 'bg-orange-400', // leave
  4: 'bg-gray-400',   // offduty
  5: 'bg-gray-300',   // offline
}

const therapistStatusLabel: Record<number, string> = {
  0: 'ออนไลน์', 1: 'กำลังนวด', 2: 'พัก', 3: 'ลา', 4: 'ออฟดิวตี้', 5: 'ออฟไลน์'
}

// สีตาม service category
const categoryColor: Record<string, string> = {
  'นวดไทย':    'bg-green-200 border-green-400 text-green-800',
  'นวดน้ำมัน': 'bg-yellow-200 border-yellow-400 text-yellow-800',
  'อโรมา':     'bg-purple-200 border-purple-400 text-purple-800',
  'ประคบ':     'bg-pink-200 border-pink-400 text-pink-800',
  'นวดเท้า':   'bg-blue-200 border-blue-400 text-blue-800',
  'คอบ่าไหล่': 'bg-orange-200 border-orange-400 text-orange-800',
}

function getDefaultColor(cat: string) {
  return categoryColor[cat] ?? 'bg-gray-200 border-gray-400 text-gray-800'
}

function pctLeft(time: string | undefined) {
  if (!time) return null
  const d = new Date(time)
  const mins = d.getHours() * 60 + d.getMinutes() - START_HOUR * 60
  return Math.max(0, Math.min(100, (mins / TOTAL_MINS) * 100))
}

function pctWidth(start: string | undefined, end: string | undefined) {
  if (!start || !end) return 0
  const s = new Date(start)
  const e = new Date(end)
  const mins = (e.getTime() - s.getTime()) / 60000
  return Math.max(0, Math.min(100, (mins / TOTAL_MINS) * 100))
}

function BlockTooltip({ item }: { item: ScheduleItem }) {
  const start = item.startTime ? new Date(item.startTime).toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit' }) : ''
  const end = item.endTime ? new Date(item.endTime).toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit' }) : ''
  return (
    <div className="absolute bottom-full left-0 mb-1 z-50 bg-gray-800 text-white text-xs rounded-lg p-2 shadow-lg whitespace-nowrap pointer-events-none">
      <p className="font-medium">{item.customerName}</p>
      <p>{item.serviceName}</p>
      <p className="text-gray-300">{start} – {end}</p>
    </div>
  )
}

interface TimelineRowProps {
  therapist: DashboardSchedule['therapists'][0]
}

function TimelineRow({ therapist }: TimelineRowProps) {
  const [hoveredItem, setHoveredItem] = useState<ScheduleItem | null>(null)

  return (
    <div className="flex items-center border-b border-gray-100 last:border-0 py-1.5">
      {/* Therapist info */}
      <div className="w-40 flex-shrink-0 flex items-center gap-2 pr-3">
        <div className="relative">
          <div className="w-8 h-8 rounded-full bg-gray-200 overflow-hidden flex items-center justify-center text-sm">
            {therapist.avatarUrl
              ? <img src={therapist.avatarUrl} className="w-full h-full object-cover" alt="" />
              : '👤'}
          </div>
          <span className={`absolute -bottom-0.5 -right-0.5 w-3 h-3 rounded-full border-2 border-white ${therapistStatusColor[therapist.currentStatus]}`} />
        </div>
        <div className="min-w-0">
          <p className="text-xs font-medium text-gray-800 truncate">{therapist.displayName}</p>
          <p className="text-xs text-gray-400">{therapistStatusLabel[therapist.currentStatus]}</p>
        </div>
      </div>

      {/* Timeline bar */}
      <div className="flex-1 relative h-9 bg-gray-50 rounded-lg overflow-hidden">
        {/* Hour grid */}
        {Array.from({ length: END_HOUR - START_HOUR + 1 }, (_, i) => (
          <div
            key={i}
            className="absolute top-0 bottom-0 border-l border-gray-200"
            style={{ left: `${(i / (END_HOUR - START_HOUR)) * 100}%` }}
          />
        ))}

        {/* Session blocks */}
        {therapist.items.map((item, idx) => {
          const left = pctLeft(item.startTime)
          const width = pctWidth(item.startTime, item.endTime)
          if (left === null || width === 0) return null
          const colors = getDefaultColor(item.serviceCategory)

          return (
            <div
              key={idx}
              className={`absolute top-1 bottom-1 rounded border ${colors} text-xs flex items-center px-1 overflow-hidden cursor-pointer`}
              style={{ left: `${left}%`, width: `${width}%` }}
              onMouseEnter={() => setHoveredItem(item)}
              onMouseLeave={() => setHoveredItem(null)}
            >
              <span className="truncate">{item.customerName}</span>
              {hoveredItem === item && <BlockTooltip item={item} />}
            </div>
          )
        })}
      </div>
    </div>
  )
}

interface TherapistTimelineProps {
  date?: string
  refreshKey?: number
}

export function TherapistTimeline({ date, refreshKey }: TherapistTimelineProps) {
  const [schedule, setSchedule] = useState<DashboardSchedule | null>(null)
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    setLoading(true)
    const params = date ? `?date=${date}` : ''
    api.get(`/dashboard/schedule${params}`)
      .then(res => setSchedule(res.data))
      .catch(console.error)
      .finally(() => setLoading(false))
  }, [date, refreshKey])

  const hours = Array.from({ length: END_HOUR - START_HOUR + 1 }, (_, i) => START_HOUR + i)

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4">
      <div className="flex items-center justify-between mb-3">
        <h3 className="text-sm font-semibold text-gray-700">ตารางงานหมอนวดวันนี้</h3>
        <div className="flex items-center gap-3 text-xs text-gray-400">
          {Object.entries(categoryColor).map(([cat, cls]) => (
            <span key={cat} className={`px-1.5 py-0.5 rounded border ${cls}`}>{cat}</span>
          ))}
          <span className="bg-gray-200 px-1.5 py-0.5 rounded border border-gray-400 text-gray-600">พัก / ว่าง</span>
        </div>
      </div>

      {/* Hour header */}
      <div className="flex mb-1 pl-40">
        <div className="flex-1 relative">
          {hours.map(h => (
            <span
              key={h}
              className="absolute text-xs text-gray-400 transform -translate-x-1/2"
              style={{ left: `${((h - START_HOUR) / (END_HOUR - START_HOUR)) * 100}%` }}
            >
              {h.toString().padStart(2, '0')}:00
            </span>
          ))}
        </div>
      </div>
      <div className="mt-4">
        {loading ? (
          <p className="text-sm text-gray-400 text-center py-8">กำลังโหลด...</p>
        ) : (schedule?.therapists ?? []).length === 0 ? (
          <p className="text-sm text-gray-400 text-center py-8">ไม่มีข้อมูลตารางงาน</p>
        ) : (
          schedule!.therapists.map(t => <TimelineRow key={t.id} therapist={t} />)
        )}
      </div>
    </div>
  )
}
