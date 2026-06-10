import { useState, useEffect } from 'react'
import { useAuthStore } from './store/authStore'
import { SignalRProvider } from './providers/SignalRProvider'
import { CleaningCheckModal } from './components/CleaningCheckModal'
import { ProfileModal } from './components/ProfileModal'
import { Sidebar, type Page } from './components/layout/Sidebar'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import WalkInPage from './pages/WalkInPage'
import QueueMonitorPage from './pages/QueueMonitorPage'
import ReportPage from './pages/ReportPage'
import RoomManagementPage from './pages/RoomManagementPage'
import FinancePage from './pages/FinancePage'
import ServicePage from './pages/ServicePage'
import CustomerPage from './pages/CustomerPage'
import TherapistPage from './pages/TherapistPage'
import UserListPage from './pages/UserListPage'
import UserPermissionsPage from './pages/UserPermissionsPage'
import { useDashboardStore } from './store/dashboardStore'

function TopBar({ onMenuToggle, isMobile, onProfileClick }: { onMenuToggle: () => void; isMobile: boolean; onProfileClick: () => void }) {
  const [isDark, setIsDark] = useState(() => localStorage.getItem('theme') === 'dark')
  const user = useAuthStore(s => s.user)

  const toggleDark = () => {
    const next = !isDark
    setIsDark(next)
    document.documentElement.classList.toggle('dark', next)
    localStorage.setItem('theme', next ? 'dark' : 'light')
  }

  return (
    <header className="h-14 bg-white dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700 flex items-center px-4 gap-3 flex-shrink-0 shadow-sm">
      {isMobile && (
        <button onClick={onMenuToggle} className="text-gray-500 p-1">☰</button>
      )}
      <div className="flex-1">
        <div className="max-w-md relative">
          <input
            type="text"
            placeholder="ค้นหาข้อมูล..."
            className="w-full pl-8 pr-3 py-1.5 text-sm bg-gray-100 dark:bg-gray-700 rounded-lg border-0 focus:outline-none focus:ring-2 focus:ring-violet-300 dark:text-gray-200"
          />
          <span className="absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400 text-sm">🔍</span>
        </div>
      </div>
      <div className="flex items-center gap-2">
        <button
          onClick={toggleDark}
          className="p-2 rounded-lg text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-700 transition"
          title="เปลี่ยนธีม"
        >
          {isDark ? '☀️' : '🌙'}
        </button>
        <button className="relative p-2 rounded-lg text-gray-500 hover:bg-gray-100 transition">
          🔔
          <span className="absolute top-1 right-1 w-2 h-2 bg-red-500 rounded-full" />
        </button>
        <button className="p-2 rounded-lg text-gray-500 hover:bg-gray-100 transition">💬</button>
        <button
          onClick={onProfileClick}
          className="w-8 h-8 rounded-full bg-violet-200 flex items-center justify-center text-sm overflow-hidden hover:ring-2 hover:ring-violet-300 transition"
          title="โปรไฟล์"
        >
          {user?.avatarUrl
            ? <img src={user.avatarUrl} alt="" className="w-full h-full object-cover" />
            : '👤'}
        </button>
      </div>
    </header>
  )
}

function AppContent() {
  const { accessToken } = useAuthStore()
  const [page, setPage] = useState<Page>('dashboard')
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null)
  const [readOnlyUser, setReadOnlyUser] = useState(false)
  const [sidebarOpen, setSidebarOpen] = useState(false)
  const [showProfile, setShowProfile] = useState(false)
  const [isMobile, setIsMobile] = useState(window.innerWidth < 768)
  const snapshot = useDashboardStore(s => s.snapshot) as any

  useEffect(() => {
    document.documentElement.classList.toggle('dark', localStorage.getItem('theme') === 'dark')
    const onResize = () => setIsMobile(window.innerWidth < 768)
    window.addEventListener('resize', onResize)
    return () => window.removeEventListener('resize', onResize)
  }, [])

  // ปิด modal ทุกอย่างเมื่อ logout (กัน state ค้างข้าม session)
  useEffect(() => {
    if (!accessToken) {
      setShowProfile(false)
      setPage('dashboard')
    }
  }, [accessToken])

  if (!accessToken) return <LoginPage />

  const planType = snapshot?.plan?.planType ?? 'Free'

  const navigate = (p: Page) => {
    setPage(p)
    setSelectedUserId(null)
    setSidebarOpen(false)
  }

  return (
    <div className="flex h-screen bg-gray-50 dark:bg-gray-900 overflow-hidden">
      <CleaningCheckModal />
      {showProfile && <ProfileModal onClose={() => setShowProfile(false)} />}

      {/* Mobile overlay */}
      {isMobile && sidebarOpen && (
        <div
          className="fixed inset-0 bg-black/40 z-30"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      {/* Sidebar */}
      <div className={`
        ${isMobile ? 'fixed left-0 top-0 bottom-0 z-40 transition-transform' : 'relative'}
        ${isMobile && !sidebarOpen ? '-translate-x-full' : 'translate-x-0'}
      `}>
        <Sidebar
          currentPage={page}
          onNavigate={navigate}
          planType={planType}
        />
      </div>

      {/* Main */}
      <div className="flex-1 flex flex-col overflow-hidden">
        <TopBar onMenuToggle={() => setSidebarOpen(o => !o)} isMobile={isMobile} onProfileClick={() => setShowProfile(true)} />
        <main className="flex-1 overflow-y-auto">
          {page === 'dashboard' && <DashboardPage onNavigate={navigate} />}
          {page === 'booking'   && <WalkInPage />}
          {page === 'schedule'  && <QueueMonitorPage />}
          {page === 'roles'     && !selectedUserId && (
            <UserListPage
              onSelectUser={(id, readonly) => {
                setSelectedUserId(id)
                setReadOnlyUser(readonly)
              }}
            />
          )}
          {page === 'roles' && selectedUserId && (
            <UserPermissionsPage
              userId={selectedUserId}
              onBack={() => setSelectedUserId(null)}
              readOnly={readOnlyUser}
            />
          )}
          {page === 'report'   && <ReportPage />}
          {page === 'revenue'  && <FinancePage />}
          {page === 'rooms'    && <RoomManagementPage />}
          {page === 'service'  && <ServicePage />}
          {page === 'customer' && <CustomerPage />}
          {page === 'therapist' && <TherapistPage />}
          {/* pages ที่ยังไม่มี component */}
          {['promotion','stock','settings','logs'].includes(page) && (
            <div className="flex items-center justify-center h-64">
              <div className="text-center text-gray-400">
                <p className="text-4xl mb-3">🚧</p>
                <p className="text-sm font-medium">อยู่ระหว่างพัฒนา</p>
                <p className="text-xs mt-1">จะเปิดให้ใช้งานเร็วๆ นี้</p>
              </div>
            </div>
          )}
        </main>
      </div>
    </div>
  )
}

function App() {
  return (
    <SignalRProvider>
      <AppContent />
    </SignalRProvider>
  )
}

export default App
