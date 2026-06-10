import { useState, useEffect } from 'react'
import { api } from '../api/client'

// ── Types ──────────────────────────────────────────
interface RevenueSummary {
  totalRevenue: number
  totalReceipts: number
  totalDiscount: number
  avgPerReceipt: number
  byMethod: { method: string; amount: number; count: number }[]
}

interface RevenueSeries {
  period: string
  label: string
  revenue: number
  receipts: number
}

interface RevenueReport {
  summary: RevenueSummary
  series: RevenueSeries[]
}

interface SummaryReport {
  bookings: { total: number; completed: number; cancelled: number; noShow: number; completionRate: number }
  walkIns: { total: number; completed: number; cancelled: number; completionRate: number }
  revenue: { total: number; fromBooking: number; fromWalkIn: number; receipts: number }
  comparison: { prevMonthRevenue: number; revenueGrowth: number; trend: string }
}

interface TherapistPerf {
  therapistId: string
  displayName: string
  code: string | null
  totalCustomers: number
  totalMins: number
  totalRevenue: number
  totalCommission: number
  avgRevenuePerCustomer: number
  topServices: { service: string; count: number }[]
}

interface PopularService {
  serviceId: string
  name: string
  count: number
  totalRevenue: number
  totalMins: number
  avgPrice: number
}

type Tab = 'summary' | 'revenue' | 'therapist' | 'services'

const MONTHS = ['ม.ค.','ก.พ.','มี.ค.','เม.ย.','พ.ค.','มิ.ย.','ก.ค.','ส.ค.','ก.ย.','ต.ค.','พ.ย.','ธ.ค.']

// ── Main ───────────────────────────────────────────
export default function ReportPage() {
  const now = new Date()
  const [tab, setTab] = useState<Tab>('summary')
  const [year, setYear] = useState(now.getFullYear())
  const [month, setMonth] = useState(now.getMonth() + 1)
  const [mode, setMode] = useState<'month' | 'range'>('month')
  const pad = (n: number) => String(n).padStart(2, '0')
  const todayStr = `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
  const [fromDt, setFromDt] = useState(`${todayStr}T00:00`)
  const [toDt, setToDt] = useState(`${todayStr}T23:59`)
  const [loading, setLoading] = useState(false)

  const [summary, setSummary] = useState<SummaryReport | null>(null)
  const [revenue, setRevenue] = useState<RevenueReport | null>(null)
  const [therapists, setTherapists] = useState<TherapistPerf[]>([])
  const [services, setServices] = useState<PopularService[]>([])

  const load = async () => {
    setLoading(true)
    try {
      const q = mode === 'range'
        ? `from=${encodeURIComponent(fromDt)}&to=${encodeURIComponent(toDt)}`
        : `year=${year}&month=${month}`
      const [s, r, t, p] = await Promise.all([
        api.get(`/report/summary?${q}`),
        api.get(`/report/revenue?${q}&groupBy=day`),
        api.get(`/report/therapist-performance?${q}`),
        api.get(`/report/popular-services?${q}`),
      ])
      setSummary(s.data)
      setRevenue(r.data)
      setTherapists(t.data.performance)
      setServices(p.data.popular)
    } catch (err) {
      console.error(err)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { if (mode === 'month') load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [year, month, mode])

  const tabs: { key: Tab; icon: string; label: string }[] = [
    { key: 'summary', icon: '📋', label: 'สรุป' },
    { key: 'revenue', icon: '💰', label: 'รายได้' },
    { key: 'therapist', icon: '💆', label: 'หมอนวด' },
    { key: 'services', icon: '⭐', label: 'บริการ' },
  ]

  return (
    <div className="max-w-2xl mx-auto px-4 py-4">

      {/* Header + Filter */}
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-lg font-bold text-gray-800 dark:text-white">📊 รายงาน</h1>
        <div className="flex flex-col items-end gap-2">
          <div className="flex items-center gap-1 bg-gray-100 dark:bg-gray-700 rounded-lg p-0.5">
            <button onClick={() => setMode('month')} className={`text-xs px-2.5 py-1 rounded-md ${mode === 'month' ? 'bg-white dark:bg-gray-600 shadow-sm font-medium' : 'text-gray-500'}`}>รายเดือน</button>
            <button onClick={() => setMode('range')} className={`text-xs px-2.5 py-1 rounded-md ${mode === 'range' ? 'bg-white dark:bg-gray-600 shadow-sm font-medium' : 'text-gray-500'}`}>ช่วงวันที่</button>
          </div>
          {mode === 'month' ? (
            <div className="flex gap-2">
              <select value={month} onChange={e => setMonth(Number(e.target.value))} className={selectClass}>
                {MONTHS.map((m, i) => (<option key={i + 1} value={i + 1}>{m}</option>))}
              </select>
              <select value={year} onChange={e => setYear(Number(e.target.value))} className={selectClass}>
                {[2024, 2025, 2026, 2027].map(y => (<option key={y} value={y}>{y}</option>))}
              </select>
            </div>
          ) : (
            <div className="flex flex-wrap items-center gap-1.5">
              <input type="datetime-local" value={fromDt} onChange={e => setFromDt(e.target.value)} className={selectClass} />
              <span className="text-xs text-gray-400">ถึง</span>
              <input type="datetime-local" value={toDt} onChange={e => setToDt(e.target.value)} className={selectClass} />
              <button onClick={load} className="px-3 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white text-xs rounded-lg">ค้นหา</button>
            </div>
          )}
        </div>
      </div>

      {/* Tabs */}
      <div className="flex bg-gray-100 dark:bg-gray-800 rounded-xl p-1 mb-4 gap-1">
        {tabs.map(t => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={`flex-1 py-1.5 text-xs font-semibold rounded-lg transition
              ${tab === t.key
                ? 'bg-white dark:bg-gray-700 text-gray-800 dark:text-white shadow-sm'
                : 'text-gray-500 hover:text-gray-700 dark:hover:text-gray-300'}`}
          >
            {t.icon} {t.label}
          </button>
        ))}
      </div>

      {loading ? (
        <div className="flex items-center justify-center h-48">
          <p className="text-gray-400 text-sm animate-pulse">กำลังโหลด...</p>
        </div>
      ) : (
        <>
          {/* ── Tab: Summary ── */}
          {tab === 'summary' && summary && (
            <div className="space-y-4">

              {/* Revenue highlight */}
              <div className="bg-gradient-to-br from-emerald-400 to-teal-500 rounded-2xl p-5 text-white">
                <p className="text-sm opacity-80 mb-1">รายได้เดือนนี้</p>
                <p className="text-4xl font-black">฿{summary.revenue.total.toLocaleString()}</p>
                <div className="flex items-center gap-2 mt-2">
                  <span className={`text-sm font-semibold ${summary.comparison.trend === 'up' ? 'text-emerald-100' : 'text-red-200'}`}>
                    {summary.comparison.trend === 'up' ? '↑' : '↓'} {Math.abs(summary.comparison.revenueGrowth)}%
                  </span>
                  <span className="text-xs opacity-70">จากเดือนก่อน</span>
                </div>
                <div className="grid grid-cols-2 gap-3 mt-3">
                  <MiniStat label="จาก Booking" value={`฿${summary.revenue.fromBooking.toLocaleString()}`} />
                  <MiniStat label="จาก Walk-in" value={`฿${summary.revenue.fromWalkIn.toLocaleString()}`} />
                </div>
              </div>

              {/* Booking + WalkIn */}
              <div className="grid grid-cols-2 gap-3">
                <Card title="📅 Booking">
                  <BigNum value={summary.bookings.total} label="ทั้งหมด" />
                  <div className="space-y-1 mt-3">
                    <StatRow label="เสร็จแล้ว" value={summary.bookings.completed} color="emerald" />

                    <StatRow label="ยกเลิก" value={summary.bookings.cancelled} color="red" />
                    <StatRow label="ไม่มา" value={summary.bookings.noShow} color="gray" />
                  </div>
                  <RateBar label="Completion" rate={summary.bookings.completionRate} />
                </Card>

                <Card title="🚶 Walk-in">
                  <BigNum value={summary.walkIns.total} label="ทั้งหมด" />
                  <div className="space-y-1 mt-3">
                    <StatRow label="เสร็จแล้ว" value={summary.walkIns.completed} color="emerald" />
                    <StatRow label="ยกเลิก" value={summary.walkIns.cancelled} color="red" />
                  </div>
                  <RateBar label="Completion" rate={summary.walkIns.completionRate} />
                </Card>
              </div>
            </div>
          )}

          {/* ── Tab: Revenue ── */}
          {tab === 'revenue' && revenue && (
            <div className="space-y-4">

              {/* Summary row */}
              <div className="grid grid-cols-3 gap-3">
                <StatCard label="รายได้รวม" value={`฿${revenue.summary.totalRevenue.toLocaleString()}`} color="emerald" />
                <StatCard label="ใบเสร็จ" value={`${revenue.summary.totalReceipts} ใบ`} color="blue" />
                <StatCard label="เฉลี่ย/ใบ" value={`฿${Math.round(revenue.summary.avgPerReceipt).toLocaleString()}`} color="purple" />
              </div>

              {/* By Method */}
              {revenue.summary.byMethod.length > 0 && (
                <Card title="💳 ช่องทางชำระ">
                  <div className="space-y-2">
                    {revenue.summary.byMethod.map(m => (
                      <div key={m.method} className="flex items-center gap-3">
                        <span className="text-xs text-gray-500 dark:text-gray-400 w-16">{m.method}</span>
                        <div className="flex-1 bg-gray-100 dark:bg-gray-700 rounded-full h-2">
                          <div
                            className="bg-emerald-500 h-2 rounded-full"
                            style={{ width: `${revenue.summary.totalRevenue > 0 ? m.amount / revenue.summary.totalRevenue * 100 : 0}%` }}
                          />
                        </div>
                        <span className="text-xs font-semibold text-gray-700 dark:text-gray-300 w-20 text-right">
                          ฿{m.amount.toLocaleString()}
                        </span>
                      </div>
                    ))}
                  </div>
                </Card>
              )}

              {/* Bar Chart */}
              {revenue.series.length > 0 ? (
                <Card title="📈 รายได้รายวัน">
                  <BarChart series={revenue.series} />
                </Card>
              ) : (
                <Empty text="ยังไม่มีข้อมูลรายได้" />
              )}
            </div>
          )}

          {/* ── Tab: Therapist ── */}
          {tab === 'therapist' && (
            <div className="space-y-3">
              {therapists.length === 0 ? <Empty text="ยังไม่มีข้อมูลหมอนวด" /> : (
                therapists.map((t, idx) => (
                  <div key={t.therapistId} className="bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 p-4 shadow-sm">
                    <div className="flex items-center gap-3 mb-3">
                      <div className={`w-8 h-8 rounded-full flex items-center justify-center font-black text-sm text-white
                        ${idx === 0 ? 'bg-yellow-400' : idx === 1 ? 'bg-gray-400' : idx === 2 ? 'bg-orange-400' : 'bg-gray-200'}`}>
                        {idx + 1}
                      </div>
                      <div className="flex-1">
                        <p className="font-semibold text-gray-800 dark:text-white text-sm">{t.displayName}</p>
                        {t.code && <p className="text-xs text-gray-400">{t.code}</p>}
                      </div>
                      <div className="text-right">
                        <p className="font-bold text-emerald-600 text-sm">฿{t.totalRevenue.toLocaleString()}</p>
                        <p className="text-xs text-gray-400">{t.totalCustomers} ลูกค้า</p>
                      </div>
                    </div>
                    <div className="grid grid-cols-3 gap-2 mb-2">
                      <MiniStatGray label="Commission" value={`฿${t.totalCommission.toLocaleString()}`} />
                      <MiniStatGray label="เวลารวม" value={`${t.totalMins} นาที`} />
                      <MiniStatGray label="เฉลี่ย/คน" value={`฿${Math.round(t.avgRevenuePerCustomer).toLocaleString()}`} />
                    </div>
                    {t.topServices.length > 0 && (
                      <div className="flex gap-1 flex-wrap">
                        {t.topServices.map(s => (
                          <span key={s.service} className="text-xs bg-emerald-50 dark:bg-emerald-900/20 text-emerald-600 dark:text-emerald-400 px-2 py-0.5 rounded-full">
                            {s.service} ({s.count})
                          </span>
                        ))}
                      </div>
                    )}
                  </div>
                ))
              )}
            </div>
          )}

          {/* ── Tab: Services ── */}
          {tab === 'services' && (
            <div className="space-y-3">
              {services.length === 0 ? <Empty text="ยังไม่มีข้อมูลบริการ" /> : (
                services.map((s, idx) => (
                  <div key={s.serviceId} className="bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 p-4 shadow-sm">
                    <div className="flex items-center gap-3 mb-2">
                      <div className={`w-8 h-8 rounded-xl flex items-center justify-center font-black text-sm
                        ${idx === 0 ? 'bg-yellow-100 text-yellow-600' : 'bg-gray-100 dark:bg-gray-700 text-gray-500'}`}>
                        #{idx + 1}
                      </div>
                      <div className="flex-1">
                        <p className="font-semibold text-gray-800 dark:text-white text-sm">{s.name}</p>
                        <p className="text-xs text-gray-400">{s.count} ครั้ง · เฉลี่ย ฿{Math.round(s.avgPrice).toLocaleString()}</p>
                      </div>
                      <p className="font-bold text-emerald-600 text-sm">฿{s.totalRevenue.toLocaleString()}</p>
                    </div>
                    {/* Popularity bar */}
                    <div className="bg-gray-100 dark:bg-gray-700 rounded-full h-1.5">
                      <div
                        className="bg-emerald-400 h-1.5 rounded-full"
                        style={{ width: `${services[0].count > 0 ? s.count / services[0].count * 100 : 0}%` }}
                      />
                    </div>
                  </div>
                ))
              )}
            </div>
          )}
        </>
      )}
    </div>
  )
}

// ── Bar Chart (CSS only) ────────────────────────────
function BarChart({ series }: { series: RevenueSeries[] }) {
  const max = Math.max(...series.map(s => s.revenue), 1)
  return (
    <div className="flex items-end gap-1 h-32 mt-2">
      {series.map(s => (
        <div key={s.period} className="flex-1 flex flex-col items-center gap-1 min-w-0">
          <div className="w-full flex flex-col justify-end" style={{ height: '100px' }}>
            <div
              className="w-full bg-emerald-400 dark:bg-emerald-500 rounded-t-sm transition-all"
              style={{ height: `${Math.max(s.revenue / max * 100, s.revenue > 0 ? 4 : 0)}%` }}
              title={`฿${s.revenue.toLocaleString()}`}
            />
          </div>
          <p className="text-xs text-gray-400 truncate w-full text-center">{s.label}</p>
        </div>
      ))}
    </div>
  )
}

// ── UI Components ───────────────────────────────────
function Card({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="bg-white dark:bg-gray-800 rounded-2xl border border-gray-100 dark:border-gray-700 p-4 shadow-sm">
      {title && <p className="text-xs font-semibold text-gray-500 dark:text-gray-400 mb-3">{title}</p>}
      {children}
    </div>
  )
}

function StatCard({ label, value, color }: { label: string; value: string; color: string }) {
  const colors: Record<string, string> = {
    emerald: 'bg-emerald-50 dark:bg-emerald-900/20 text-emerald-600 dark:text-emerald-400',
    blue: 'bg-blue-50 dark:bg-blue-900/20 text-blue-600 dark:text-blue-400',
    purple: 'bg-purple-50 dark:bg-purple-900/20 text-purple-600 dark:text-purple-400',
  }
  return (
    <div className={`rounded-xl p-3 text-center ${colors[color]}`}>
      <p className="text-lg font-black">{value}</p>
      <p className="text-xs opacity-70 mt-0.5">{label}</p>
    </div>
  )
}

function BigNum({ value, label }: { value: number; label: string }) {
  return (
    <div className="text-center">
      <p className="text-3xl font-black text-gray-800 dark:text-white">{value}</p>
      <p className="text-xs text-gray-400">{label}</p>
    </div>
  )
}

function StatRow({ label, value, color }: { label: string; value: number; color: string }) {
  const colors: Record<string, string> = {
    emerald: 'text-emerald-600', blue: 'text-blue-500',
    red: 'text-red-500', gray: 'text-gray-400'
  }
  return (
    <div className="flex justify-between text-xs">
      <span className="text-gray-500 dark:text-gray-400">{label}</span>
      <span className={`font-semibold ${colors[color]}`}>{value}</span>
    </div>
  )
}

function RateBar({ label, rate }: { label: string; rate: number }) {
  return (
    <div className="mt-3">
      <div className="flex justify-between text-xs mb-1">
        <span className="text-gray-400">{label}</span>
        <span className="font-semibold text-emerald-600">{rate}%</span>
      </div>
      <div className="bg-gray-100 dark:bg-gray-700 rounded-full h-1.5">
        <div className="bg-emerald-400 h-1.5 rounded-full" style={{ width: `${rate}%` }} />
      </div>
    </div>
  )
}

function MiniStat({ label, value }: { label: string; value: string }) {
  return (
    <div className="bg-white/20 rounded-lg p-2 text-center">
      <p className="text-sm font-bold">{value}</p>
      <p className="text-xs opacity-70">{label}</p>
    </div>
  )
}

function MiniStatGray({ label, value }: { label: string; value: string }) {
  return (
    <div className="bg-gray-50 dark:bg-gray-700 rounded-lg p-2 text-center">
      <p className="text-xs font-bold text-gray-700 dark:text-gray-300">{value}</p>
      <p className="text-xs text-gray-400">{label}</p>
    </div>
  )
}

function Empty({ text }: { text: string }) {
  return (
    <div className="text-center py-12 text-gray-400 text-sm">{text}</div>
  )
}

const selectClass = "border border-gray-300 dark:border-gray-600 rounded-lg px-2 py-1.5 text-xs bg-white dark:bg-gray-700 text-gray-800 dark:text-white focus:outline-none focus:border-emerald-400"