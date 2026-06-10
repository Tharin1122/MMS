import { useEffect, useState } from 'react'
import { api } from '../api/client'

interface Booking {
  id: string; bookingNo: string; startTime?: string; endTime?: string
  totalAmount: number; status: number; itemCount: number
  customer: { displayName: string; phone?: string }
}

const STATUS = ['รอยืนยัน', 'ยืนยันแล้ว', 'กำลังบริการ', 'เสร็จสิ้น', 'ยกเลิก', 'ไม่มา']
const STATUS_COLOR = ['bg-amber-100 text-amber-700', 'bg-blue-100 text-blue-700', 'bg-violet-100 text-violet-700', 'bg-emerald-100 text-emerald-700', 'bg-red-100 text-red-700', 'bg-gray-200 text-gray-500']

export function DayBookingsModal({ date, onClose, onGoToBookings }: { date: string; onClose: () => void; onGoToBookings?: () => void }) {
  const [items, setItems] = useState<Booking[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    api.get(`/booking?date=${date}&pageSize=100`)
      .then(res => setItems(res.data.items))
      .catch(() => setItems([]))
      .finally(() => setLoading(false))
  }, [date])

  const d = new Date(date + 'T00:00:00')
  const label = d.toLocaleDateString('th-TH', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })
  const fmt = (t?: string) => t ? t.slice(0, 5) : '--:--'

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-xl w-full max-w-md max-h-[85vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100 dark:border-gray-700 sticky top-0 bg-white dark:bg-gray-800">
          <div>
            <h2 className="text-base font-bold text-gray-800 dark:text-white">📅 คิว/การจอง</h2>
            <p className="text-xs text-gray-400">{label}</p>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl">✕</button>
        </div>

        <div className="p-4">
          {loading ? (
            <p className="text-sm text-gray-400 text-center py-8">กำลังโหลด...</p>
          ) : items.length === 0 ? (
            <div className="text-center py-8">
              <p className="text-3xl mb-2">📭</p>
              <p className="text-sm text-gray-400">ไม่มีการจองในวันนี้</p>
            </div>
          ) : (
            <div className="space-y-2">
              {items.map(b => (
                <div key={b.id} className="flex items-center justify-between border border-gray-100 dark:border-gray-700 rounded-xl px-3 py-2.5">
                  <div className="min-w-0">
                    <p className="text-sm font-medium dark:text-white truncate">{b.customer.displayName}</p>
                    <p className="text-xs text-gray-400">{b.bookingNo} · {fmt(b.startTime)}-{fmt(b.endTime)} · {b.itemCount} รายการ</p>
                  </div>
                  <div className="text-right flex-shrink-0 ml-2">
                    <span className={`text-xs px-2 py-0.5 rounded-full ${STATUS_COLOR[b.status] ?? ''}`}>{STATUS[b.status] ?? '-'}</span>
                    <p className="text-xs font-medium text-gray-700 dark:text-gray-200 mt-1">฿{b.totalAmount.toLocaleString('th-TH')}</p>
                  </div>
                </div>
              ))}
            </div>
          )}

          {onGoToBookings && (
            <button onClick={onGoToBookings} className="w-full mt-4 py-2.5 bg-violet-600 hover:bg-violet-700 text-white text-sm font-medium rounded-lg transition">
              ไปหน้าการจอง & คิวงาน →
            </button>
          )}
        </div>
      </div>
    </div>
  )
}
