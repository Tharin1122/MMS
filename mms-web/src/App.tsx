import { useState, useEffect } from 'react'
import { useAuthStore } from './store/authStore'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'

function App() {
  const { accessToken } = useAuthStore()
  const [isDark, setIsDark] = useState(() =>
    localStorage.getItem('theme') === 'dark'
  )

  useEffect(() => {
    document.documentElement.classList.toggle('dark', isDark)
    localStorage.setItem('theme', isDark ? 'dark' : 'light')
  }, [isDark])

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900 transition-colors">

      {accessToken ? <DashboardPage /> : <LoginPage />}
    </div>
  )
}

export default App