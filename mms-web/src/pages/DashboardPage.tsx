// import { useEffect } from 'react'
import { useAuthStore } from '../store/authStore'
import { useDashboardStore } from '../store/dashboardStore'
import { useSignalR } from '../hooks/useSignalR'
import { api } from '../api/client'
import { t } from '../i18n/th'
import { useEffect, useState } from 'react'

const statusLabel: Record<number, string> = {
  0: 'ว่าง', 1: 'กำลังนวด', 2: 'พัก', 3: 'ลา', 4: 'ออฟดิวตี้', 5: 'ออฟไลน์'
}

const statusColor: Record<number, string> = {
  0: 'bg-green-500',
  1: 'bg-blue-500',
  2: 'bg-yellow-500',
  3: 'bg-orange-500',
  4: 'bg-gray-400',
  5: 'bg-gray-300',
}

const roomStatusLabel: Record<number, string> = {
  0: 'ว่าง', 1: 'ใช้งาน', 2: 'ทำความสะอาด', 3: 'ซ่อมบำรุง', 4: 'ปิด'
}

const roomStatusColor: Record<number, string> = {
  0: 'bg-green-500',
  1: 'bg-blue-500',
  2: 'bg-yellow-400',
  3: 'bg-red-400',
  4: 'bg-gray-400',
}

export default function DashboardPage() {
  const { user, logout } = useAuthStore()
  const { snapshot, isLoading, lastUpdated, setSnapshot, setLoading } = useDashboardStore()
  useSignalR()

  const [isDark, setIsDark] = useState(() =>
    document.documentElement.classList.contains('dark')
  )
  const toggleDark = () => {
    setIsDark(p => !p)
    document.documentElement.classList.toggle('dark')
    localStorage.setItem('theme', isDark ? 'light' : 'dark')
  }

  const fetchSnapshot = async () => {
    setLoading(true)
    try {
      const res = await api.get('/dashboard')
      setSnapshot(res.data)
    } catch (err) {
      console.error(err)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchSnapshot()
    // auto refresh ทุก 30 วิ เผื่อ SignalR หลุด
    const interval = setInterval(fetchSnapshot, 30000)
    return () => clearInterval(interval)
  }, [])

  if (isLoading && !snapshot)
    return (
      <div className="min-h-screen flex items-center justify-center">
        <p className="text-gray-500 dark:text-gray-400">{t('common.loading')}</p>
      </div>
    )

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900 pb-10">

      {/* Header */}
      <div className="bg-white dark:bg-gray-800 shadow-sm px-6 py-4 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-gray-800 dark:text-white">
            🌿 {t('dashboard.title')}
          </h1>
          <p className="text-xs text-gray-400 mt-0.5">
            {lastUpdated ? `อัปเดต ${lastUpdated.toLocaleTimeString('th-TH')}` : ''}
          </p>
        </div>
        <div className="flex items-center gap-3">
        
          <span className="text-sm text-gray-600 dark:text-gray-300">
            {user?.displayName}
          </span>
          {/* Dark mode toggle */}
          <button
            onClick={toggleDark}
            className="p-2 rounded-full bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-600 transition"
            title="เปลี่ยนธีม"
          >
            {isDark ? '☀️' : '🌙'}
          </button>
          <button
            onClick={logout}
            className="text-xs text-red-500 hover:text-red-700 transition"
          >
            {t('auth.logout')}
          </button>
        </div>
      </div>

      <div className="max-w-7xl mx-auto px-4 py-6 space-y-6">

        {/* Revenue + Stats Row */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <StatCard
            label={t('dashboard.revenue')}
            value={`฿${(snapshot?.revenue.totalRevenue ?? 0).toLocaleString()}`}
            icon="💰"
            color="green"
          />
          <StatCard
            label={t('dashboard.customers')}
            value={`${snapshot?.queue.totalToday ?? 0} คน`}
            icon="👥"
            color="blue"
          />
          <StatCard
            label="หมอนวดว่าง"
            value={`${snapshot?.therapists.available ?? 0} / ${snapshot?.therapists.total ?? 0}`}
            icon="💆"
            color="emerald"
          />
          <StatCard
            label="ห้องว่าง"
            value={`${snapshot?.rooms.available ?? 0} / ${snapshot?.rooms.total ?? 0}`}
            icon="🏠"
            color="purple"
          />
        </div>

        {/* Therapists */}
        <Section title={`💆 หมอนวด (${snapshot?.therapists.total ?? 0} คน)`}>
          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-3">
            {snapshot?.therapists.list.map(t => (
              <div
                key={t.id}
                className="bg-white dark:bg-gray-800 rounded-xl p-3 text-center shadow-sm border border-gray-100 dark:border-gray-700"
              >
                <div className="w-12 h-12 rounded-full bg-gray-200 dark:bg-gray-600 mx-auto mb-2 flex items-center justify-center text-xl overflow-hidden">
                  {t.avatarUrl ? (
                    <img src={t.avatarUrl} alt={t.displayName} className="w-full h-full object-cover rounded-full" />
                  ) : '👤'}
                </div>
                <p className="text-xs font-medium text-gray-800 dark:text-white truncate">
                  {t.displayName}
                </p>
                <span className={`inline-block mt-1 px-2 py-0.5 rounded-full text-white text-xs ${statusColor[t.currentStatus]}`}>
                  {statusLabel[t.currentStatus]}
                </span>
              </div>
            ))}
          </div>
        </Section>

        {/* Rooms */}
        <Section title={`🏠 ห้องนวด (${snapshot?.rooms.total ?? 0} ห้อง)`}>
          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-3">
            {snapshot?.rooms.list.map(r => (
              <div
                key={r.id}
                className="bg-white dark:bg-gray-800 rounded-xl p-3 text-center shadow-sm border border-gray-100 dark:border-gray-700"
              >
                <div className="text-3xl mb-2">🚪</div>
                <p className="text-xs font-medium text-gray-800 dark:text-white">{r.name}</p>
                <span className={`inline-block mt-1 px-2 py-0.5 rounded-full text-white text-xs ${roomStatusColor[r.currentStatus]}`}>
                  {roomStatusLabel[r.currentStatus]}
                </span>
              </div>
            ))}
          </div>
        </Section>

        {/* Queue */}
        <Section title={`🎫 คิวรอ (${snapshot?.queue.waiting ?? 0} คิว)`}>
          {(snapshot?.queue.waitingList.length ?? 0) === 0 ? (
            <p className="text-gray-400 text-sm text-center py-4">{t('queue.noWaiting')}</p>
          ) : (
            <div className="space-y-2">
              {snapshot?.queue.waitingList.map(q => (
                <div
                  key={q.id}
                  className="bg-white dark:bg-gray-800 rounded-xl px-4 py-3 flex items-center justify-between shadow-sm border border-gray-100 dark:border-gray-700"
                >
                  <div className="flex items-center gap-3">
                    <span className="text-2xl font-bold text-emerald-600 dark:text-emerald-400 w-14">
                      {q.queueNo}
                    </span>
                    <div>
                      <p className="text-sm font-medium text-gray-800 dark:text-white">
                        {q.customer.displayName}
                      </p>
                      <p className="text-xs text-gray-400">
                        {q.serviceCount} บริการ
                      </p>
                    </div>
                  </div>
                  {q.estimatedWaitMins != null && (
                    <div className="text-right">
                      <p className="text-xs text-gray-400">{t('queue.estimatedWait')}</p>
                      <p className="text-sm font-bold text-orange-500">
                        ~{q.estimatedWaitMins} {t('queue.minutes')}
                      </p>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </Section>

        {/* In Service */}
        {(snapshot?.queue.inServiceList.length ?? 0) > 0 && (
          <Section title={`⚡ กำลังบริการ (${snapshot?.queue.inService ?? 0} คิว)`}>
            <div className="space-y-2">
              {snapshot?.queue.inServiceList.map(q => (
                <div
                  key={q.id}
                  className="bg-blue-50 dark:bg-blue-900/20 rounded-xl px-4 py-3 flex items-center justify-between border border-blue-100 dark:border-blue-800"
                >
                  <div className="flex items-center gap-3">
                    <span className="text-2xl font-bold text-blue-600 dark:text-blue-400 w-14">
                      {q.queueNo}
                    </span>
                    <div>
                      <p className="text-sm font-medium text-gray-800 dark:text-white">
                        {q.customer.displayName}
                      </p>
                      <p className="text-xs text-gray-400">
                        เริ่ม {q.startTime ? new Date(q.startTime).toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit' }) : '-'}
                        {' · '}
                        เสร็จ {q.endTime ? new Date(q.endTime).toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit' }) : '-'}
                      </p>
                    </div>
                  </div>
                  <span className="text-xs bg-blue-500 text-white px-2 py-1 rounded-full">
                    กำลังบริการ
                  </span>
                </div>
              ))}
            </div>
          </Section>
        )}
      </div>
    </div>
  )
}

function StatCard({ label, value, icon, color }: {
  label: string, value: string, icon: string, color: string
}) {
  const colorMap: Record<string, string> = {
    green: 'from-green-400 to-emerald-500',
    blue: 'from-blue-400 to-blue-500',
    emerald: 'from-emerald-400 to-teal-500',
    purple: 'from-purple-400 to-purple-500',
  }
  return (
    <div className={`bg-gradient-to-br ${colorMap[color]} rounded-xl p-4 text-white shadow`}>
      <div className="text-2xl mb-1">{icon}</div>
      <p className="text-2xl font-bold">{value}</p>
      <p className="text-xs opacity-80 mt-0.5">{label}</p>
    </div>
  )
}

function Section({ title, children }: { title: string, children: React.ReactNode }) {
  return (
    <div>
      <h2 className="text-sm font-semibold text-gray-600 dark:text-gray-400 mb-3">{title}</h2>
      {children}
    </div>
  )
}