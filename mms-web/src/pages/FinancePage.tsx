import { useEffect, useState } from 'react'
import { api } from '../api/client'

interface RevenueSeries { period: string; label: string; revenue: number; receipts: number; discount: number }
interface ByMethod { method: string; amount: number; count: number }
interface RevenueData {
  year: number; month: number
  summary: { totalRevenue: number; totalReceipts: number; totalDiscount: number; avgPerReceipt: number; byMethod: ByMethod[] }
  series: RevenueSeries[]
}

const METHOD_LABEL: Record<string, string> = {
  Cash: 'เงินสด', Transfer: 'โอน', PromptPay: 'พร้อมเพย์', Card: 'บัตร', Other: 'อื่นๆ',
}
const baht = (n: number) => n.toLocaleString('th-TH', { minimumFractionDigits: 0 })

export default function FinancePage() {
  const now = new Date()
  const [year, setYear] = useState(now.getFullYear())
  const [month, setMonth] = useState(now.getMonth() + 1)
  const [data, setData] = useState<RevenueData | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    setLoading(true); setError('')
    api.get(`/report/revenue?year=${year}&month=${month}&groupBy=day`)
      .then(res => setData(res.data))
      .catch(err => setError(err?.response?.data?.message ?? 'โหลดข้อมูลการเงินไม่สำเร็จ'))
      .finally(() => setLoading(false))
  }, [year, month])

  const thMonths = ['ม.ค.','ก.พ.','มี.ค.','เม.ย.','พ.ค.','มิ.ย.','ก.ค.','ส.ค.','ก.ย.','ต.ค.','พ.ย.','ธ.ค.']
  const maxRev = data?.series.length ? Math.max(...data.series.map(s => s.revenue)) : 0

  return (
    <div className="p-4 space-y-4">
      <div className="flex items-center justify-between flex-wrap gap-2">
        <h1 className="text-lg font-bold dark:text-white">💰 การเงิน</h1>
        <div className="flex gap-2">
          <select value={month} onChange={e => setMonth(Number(e.target.value))}
            className="text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-2 py-1.5 bg-white dark:bg-gray-700 dark:text-white">
            {thMonths.map((m, i) => <option key={i} value={i + 1}>{m}</option>)}
          </select>
          <select value={year} onChange={e => setYear(Number(e.target.value))}
            className="text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-2 py-1.5 bg-white dark:bg-gray-700 dark:text-white">
            {[0, 1, 2].map(d => { const y = now.getFullYear() - d; return <option key={y} value={y}>{y + 543}</option> })}
          </select>
        </div>
      </div>

      {loading && <p className="text-sm text-gray-400 text-center py-10">กำลังโหลด...</p>}
      {error && <p className="text-sm text-red-500 text-center py-10">{error}</p>}

      {data && !loading && (
        <>
          {/* Summary cards */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
            <StatCard label="รายรับรวม" value={`฿${baht(data.summary.totalRevenue)}`} accent="emerald" />
            <StatCard label="จำนวนบิล" value={`${data.summary.totalReceipts}`} accent="violet" />
            <StatCard label="เฉลี่ย/บิล" value={`฿${baht(Math.round(data.summary.avgPerReceipt))}`} accent="blue" />
            <StatCard label="ส่วนลดรวม" value={`฿${baht(data.summary.totalDiscount)}`} accent="amber" />
          </div>

          {/* By payment method */}
          <div className="bg-white dark:bg-gray-800 rounded-xl p-4 shadow-sm">
            <h2 className="text-sm font-semibold text-gray-700 dark:text-gray-200 mb-3">แยกตามวิธีชำระเงิน</h2>
            {data.summary.byMethod.length === 0 ? (
              <p className="text-sm text-gray-400 text-center py-4">ยังไม่มีรายการชำระเงินในเดือนนี้</p>
            ) : (
              <div className="space-y-2">
                {data.summary.byMethod.map(m => (
                  <div key={m.method} className="flex items-center justify-between text-sm">
                    <span className="text-gray-600 dark:text-gray-300">{METHOD_LABEL[m.method] ?? m.method} <span className="text-gray-400">({m.count})</span></span>
                    <span className="font-medium dark:text-white">฿{baht(m.amount)}</span>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Daily series */}
          <div className="bg-white dark:bg-gray-800 rounded-xl p-4 shadow-sm">
            <h2 className="text-sm font-semibold text-gray-700 dark:text-gray-200 mb-3">รายรับรายวัน</h2>
            {data.series.length === 0 ? (
              <p className="text-sm text-gray-400 text-center py-4">ยังไม่มีรายรับในเดือนนี้</p>
            ) : (
              <div className="space-y-1.5">
                {data.series.map(s => (
                  <div key={s.period} className="flex items-center gap-2 text-xs">
                    <span className="w-16 text-gray-500 dark:text-gray-400 flex-shrink-0">{s.label}</span>
                    <div className="flex-1 bg-gray-100 dark:bg-gray-700 rounded-full h-4 overflow-hidden">
                      <div className="bg-emerald-400 h-full rounded-full" style={{ width: `${maxRev ? (s.revenue / maxRev) * 100 : 0}%` }} />
                    </div>
                    <span className="w-20 text-right font-medium dark:text-white flex-shrink-0">฿{baht(s.revenue)}</span>
                    <span className="w-8 text-right text-gray-400 flex-shrink-0">{s.receipts}บิล</span>
                  </div>
                ))}
              </div>
            )}
          </div>
        </>
      )}
    </div>
  )
}

function StatCard({ label, value, accent }: { label: string; value: string; accent: string }) {
  const colors: Record<string, string> = {
    emerald: 'text-emerald-600', violet: 'text-violet-600', blue: 'text-blue-600', amber: 'text-amber-600',
  }
  return (
    <div className="bg-white dark:bg-gray-800 rounded-xl p-4 shadow-sm">
      <p className="text-xs text-gray-400 mb-1">{label}</p>
      <p className={`text-xl font-bold ${colors[accent]}`}>{value}</p>
    </div>
  )
}
