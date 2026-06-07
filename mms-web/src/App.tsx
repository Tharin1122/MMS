import { useState, useEffect } from 'react'
import { useAuthStore } from './store/authStore'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import WalkInPage from './pages/WalkInPage'
import QueueMonitorPage from './pages/QueueMonitorPage'

type Page = 'dashboard' | 'walkin' | 'queue'

function App() {
  const { accessToken } = useAuthStore()
  const [page, setPage] = useState<Page>('dashboard')
  const [isDark, setIsDark] = useState(() =>
    localStorage.getItem('theme') === 'dark'
  )

  useEffect(() => {
    document.documentElement.classList.toggle('dark', isDark)
    localStorage.setItem('theme', isDark ? 'dark' : 'light')
  }, [isDark])

  if (!accessToken) return <LoginPage />

  const navItems: { key: Page; icon: string; label: string }[] = [
    { key: 'dashboard', icon: '📊', label: 'ภาพรวม' },
    { key: 'walkin', icon: '🚶', label: 'รับลูกค้า' },
    { key: 'queue', icon: '🎫', label: 'คิว' },
  ]

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900 transition-colors">

      {/* Bottom Nav */}
      <nav className="fixed bottom-0 left-0 right-0 bg-white dark:bg-gray-800 border-t border-gray-200 dark:border-gray-700 z-50 flex">
        {navItems.map(item => (
          <button
            key={item.key}
            onClick={() => setPage(item.key)}
            className={`flex-1 py-3 text-xs flex flex-col items-center gap-1 transition
              ${page === item.key
                ? 'text-emerald-500'
                : 'text-gray-400 hover:text-gray-600 dark:hover:text-gray-300'}`}
          >
            <span className="text-xl">{item.icon}</span>
            {item.label}
          </button>
        ))}
        <button
          onClick={() => setIsDark(p => !p)}
          className="flex-1 py-3 text-xs flex flex-col items-center gap-1 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 transition"
        >
          <span className="text-xl">{isDark ? '☀️' : '🌙'}</span>
          ธีม
        </button>
      </nav>

      {/* Content */}
      <div className="pb-20">
        {page === 'dashboard' && <DashboardPage />}
        {page === 'walkin' && <WalkInPage />}
        {page === 'queue' && <QueueMonitorPage />}
      </div>
    </div>
  )
}

export default App
