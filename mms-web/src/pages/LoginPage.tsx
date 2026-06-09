import { useState, useEffect } from 'react'
import { useAuthStore } from '../store/authStore'
import { api } from '../api/client'
import { t } from '../i18n/th'
import { lineLogin, initLiff, liff, isLiffConfigured } from '../lib/liff'

export default function LoginPage() {
  const { login } = useAuthStore()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [devId, setDevId] = useState('Uowner_demo_001')
  const [showDev, setShowDev] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [linkTargetName, setLinkTargetName] = useState<string | null>(null)

  const applyLogin = (data: any) => {
    const { accessToken, refreshToken, user, permissions } = data
    login(accessToken, refreshToken ?? '', user, permissions)
  }

  // อ่าน ?link=TOKEN จาก URL (พนักงานสแกน QR ผูกบัญชี)
  const linkToken = new URLSearchParams(window.location.search).get('link')

  const completeLineLogin = async (lineAccessToken: string) => {
    setLoading(true)
    setError('')
    try {
      // ถ้ามี link token → ผูกบัญชี, ไม่งั้น → login ปกติ
      const endpoint = linkToken ? '/auth/link-line' : '/auth/line-login'
      const body = linkToken
        ? { linkToken, accessToken: lineAccessToken }
        : { accessToken: lineAccessToken }
      const res = await api.post(endpoint, body)
      applyLogin(res.data)
      // เคลียร์ link param ออกจาก URL
      if (linkToken) window.history.replaceState({}, '', window.location.pathname)
    } catch (err: any) {
      setError(err?.response?.data?.message ?? 'เข้าสู่ระบบด้วย LINE ไม่สำเร็จ')
    } finally {
      setLoading(false)
    }
  }

  // ดึงชื่อพนักงานจาก link token (แสดงบน banner)
  useEffect(() => {
    if (!linkToken) return
    api.get(`/auth/link-info/${linkToken}`)
      .then(res => setLinkTargetName(res.data.targetName))
      .catch(() => setError('ลิงก์ผูกบัญชีหมดอายุหรือถูกใช้แล้ว'))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // หลัง LIFF redirect กลับมา — ดึง token แล้ว login ต่อ
  useEffect(() => {
    if (!isLiffConfigured()) return
    initLiff()
      .then(ok => {
        if (ok && liff.isLoggedIn()) {
          const token = liff.getAccessToken()
          if (token) completeLineLogin(token)
        }
      })
      .catch(() => {})
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const handleLineLogin = async () => {
    if (!isLiffConfigured()) {
      setError('ยังไม่ได้ตั้งค่า LINE LIFF (VITE_LIFF_ID)')
      return
    }
    setLoading(true)
    setError('')
    try {
      const token = await lineLogin()
      if (token) await completeLineLogin(token)
      // ถ้า token เป็น null = กำลัง redirect ไป LINE
    } catch (err: any) {
      setError(err?.message ?? 'เชื่อม LINE ไม่สำเร็จ')
      setLoading(false)
    }
  }

  const handlePasswordLogin = async () => {
    if (!username || !password) {
      setError('กรุณากรอกชื่อผู้ใช้และรหัสผ่าน')
      return
    }
    setLoading(true)
    setError('')
    try {
      const res = await api.post('/auth/login', { username, password })
      applyLogin(res.data)
    } catch (err: any) {
      setError(err?.response?.data?.message ?? 'เข้าสู่ระบบไม่สำเร็จ')
    } finally {
      setLoading(false)
    }
  }

  const handleDevLogin = async () => {
    setLoading(true)
    setError('')
    try {
      const res = await api.post('/auth/line-login', { lineUserId: devId })
      applyLogin(res.data)
    } catch {
      setError('เข้าสู่ระบบไม่สำเร็จ')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-green-50 to-emerald-100 dark:from-gray-900 dark:to-gray-800 p-4">
      <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-xl p-8 w-full max-w-sm">

        {/* Logo */}
        <div className="text-center mb-7">
          <div className="text-5xl mb-3">🌿</div>
          <h1 className="text-2xl font-bold text-gray-800 dark:text-white">
            {t('auth.login.title')}
          </h1>
          <p className="text-gray-500 dark:text-gray-400 text-sm mt-1">
            {t('auth.login.subtitle')}
          </p>
        </div>

        {/* Link account banner */}
        {linkTargetName && (
          <div className="mb-4 bg-emerald-50 dark:bg-emerald-900/30 border border-emerald-200 dark:border-emerald-800 rounded-xl p-3 text-center">
            <p className="text-sm font-medium text-emerald-800 dark:text-emerald-200">
              🔗 ผูกบัญชี LINE สำหรับ
            </p>
            <p className="text-base font-bold text-emerald-900 dark:text-emerald-100">{linkTargetName}</p>
            <p className="text-xs text-emerald-600 dark:text-emerald-400 mt-1">กดปุ่มด้านล่างเพื่อยืนยันด้วย LINE</p>
          </div>
        )}

        {/* LINE Login Button */}
        <button
          onClick={handleLineLogin}
          disabled={loading}
          className="w-full bg-[#06C755] hover:bg-[#05b34c] text-white font-semibold py-3 px-4 rounded-xl flex items-center justify-center gap-3 mb-5 transition disabled:opacity-50"
        >
          <svg viewBox="0 0 24 24" className="w-6 h-6 fill-white">
            <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm4.64 6.8c-.15 1.58-.8 5.42-1.13 7.19-.14.75-.42 1-.68 1.03-.58.05-1.02-.38-1.58-.75-.88-.58-1.38-.94-2.23-1.5-.99-.65-.35-1.01.22-1.59.15-.15 2.71-2.48 2.76-2.69.01-.03.01-.14-.07-.2-.08-.06-.19-.04-.27-.02-.12.02-1.96 1.25-5.54 3.69-.52.36-1 .53-1.42.52-.47-.01-1.37-.26-2.03-.48-.82-.27-1.47-.42-1.42-.88.03-.24.37-.49 1.02-.75 3.98-1.73 6.64-2.87 7.97-3.43 3.79-1.63 4.58-1.91 5.09-1.92.11 0 .37.03.53.17.14.12.18.28.2.46-.02.06-.02.12-.03.18z"/>
          </svg>
          {t('auth.login.line')}
        </button>

        {/* Divider */}
        <div className="flex items-center gap-3 mb-5">
          <div className="flex-1 h-px bg-gray-200 dark:bg-gray-700" />
          <span className="text-xs text-gray-400">หรือเข้าด้วยรหัสผ่าน</span>
          <div className="flex-1 h-px bg-gray-200 dark:bg-gray-700" />
        </div>

        {/* Username/Password */}
        <div className="space-y-3">
          <input
            type="text"
            value={username}
            onChange={e => setUsername(e.target.value)}
            placeholder="ชื่อผู้ใช้"
            autoComplete="username"
            className="w-full text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2.5 bg-white dark:bg-gray-700 text-gray-800 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-300"
          />
          <input
            type="password"
            value={password}
            onChange={e => setPassword(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handlePasswordLogin()}
            placeholder="รหัสผ่าน"
            autoComplete="current-password"
            className="w-full text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2.5 bg-white dark:bg-gray-700 text-gray-800 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-300"
          />
          <button
            onClick={handlePasswordLogin}
            disabled={loading}
            className="w-full py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white text-sm font-semibold rounded-lg disabled:opacity-50 transition"
          >
            {loading ? '...' : 'เข้าสู่ระบบ'}
          </button>
        </div>

        {error && (
          <p className="text-red-500 text-xs mt-3 text-center">{error}</p>
        )}

        {/* Dev Login (collapsible) */}
        <div className="border-t border-gray-200 dark:border-gray-700 mt-5 pt-3">
          <button
            onClick={() => setShowDev(s => !s)}
            className="text-xs text-gray-400 hover:text-gray-600 w-full text-center"
          >
            {showDev ? '▲ ซ่อน' : '▼ ทดสอบ (Dev)'}
          </button>
          {showDev && (
            <div className="flex gap-2 mt-3">
              <select
                value={devId}
                onChange={e => setDevId(e.target.value)}
                className="flex-1 text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 bg-white dark:bg-gray-700 text-gray-800 dark:text-white"
              >
                <option value="Uowner_demo_001">Owner</option>
                <option value="Utherapist_demo_001">Therapist (มิ้นท์)</option>
              </select>
              <button
                onClick={handleDevLogin}
                disabled={loading}
                className="px-4 py-2 bg-gray-800 dark:bg-gray-600 text-white text-sm rounded-lg hover:bg-gray-700 disabled:opacity-50 transition"
              >
                {loading ? '...' : 'เข้า'}
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
