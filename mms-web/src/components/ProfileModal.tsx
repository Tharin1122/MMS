import { useEffect, useState } from 'react'
import { useAuthStore } from '../store/authStore'
import { api } from '../api/client'

type FieldKey = 'username' | 'password' | 'confirm'

export function ProfileModal({ onClose }: { onClose: () => void }) {
  const { logout } = useAuthStore()

  const [loaded, setLoaded] = useState(false)
  const [avatarUrl, setAvatarUrl] = useState<string | null>(null)
  const [displayName, setDisplayName] = useState('')
  const [username, setUsername] = useState('')
  const [origUsername, setOrigUsername] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [phone, setPhone] = useState('')
  const [hasPassword, setHasPassword] = useState(false)
  const [hasLine, setHasLine] = useState(false)

  const [loading, setLoading] = useState(false)
  const [invalid, setInvalid] = useState<Set<FieldKey>>(new Set())
  const [msg, setMsg] = useState<{ type: 'ok' | 'err'; text: string } | null>(null)

  // ดึงข้อมูลโปรไฟล์สดจาก DB ทุกครั้งที่เปิด
  useEffect(() => {
    api.get('/auth/me')
      .then(res => {
        const d = res.data
        setDisplayName(d.displayName ?? '')
        setUsername(d.username ?? '')
        setOrigUsername(d.username ?? '')
        setPhone(d.phone ?? '')
        setAvatarUrl(d.avatarUrl ?? null)
        setHasPassword(!!d.hasPassword)
        setHasLine(!!d.hasLine)
      })
      .catch(() => setMsg({ type: 'err', text: 'โหลดข้อมูลโปรไฟล์ไม่สำเร็จ' }))
      .finally(() => setLoaded(true))
  }, [])

  const save = async () => {
    setMsg(null)
    const bad = new Set<FieldKey>()

    // Validation
    if (username.trim() && username.trim().length < 3) bad.add('username')
    if (username.trim() && !/^[a-zA-Z0-9_]+$/.test(username.trim())) bad.add('username')
    if (username.trim() && !hasPassword && !password) bad.add('password')
    if (password && password.length < 6) bad.add('password')
    if (password && password !== confirmPassword) bad.add('confirm')

    if (bad.size > 0) {
      setInvalid(bad)
      let text = 'ข้อมูลไม่ถูกต้อง'
      if (bad.has('username')) text = 'ชื่อผู้ใช้: อย่างน้อย 3 ตัว ใช้ตัวอักษร ตัวเลข _ เท่านั้น'
      else if (bad.has('password') && !hasPassword && !password) text = 'กรุณาตั้งรหัสผ่านด้วย เพื่อใช้เข้าระบบด้วยชื่อผู้ใช้นี้'
      else if (bad.has('password')) text = 'รหัสผ่านต้องยาวอย่างน้อย 6 ตัวอักษร'
      else if (bad.has('confirm')) text = 'รหัสผ่านไม่ตรงกัน'
      setMsg({ type: 'err', text })
      return
    }

    setInvalid(new Set())
    setLoading(true)
    try {
      const body: Record<string, string> = {}
      if (displayName.trim()) body.displayName = displayName.trim()
      if (username.trim() && username.trim() !== origUsername) body.username = username.trim()
      if (password) body.password = password
      body.phone = phone.trim()

      await api.post('/auth/set-credentials', body)
      setMsg({ type: 'ok', text: 'บันทึกสำเร็จ — ครั้งหน้าเข้าด้วยรหัสผ่านได้เลย' })
      if (password) setHasPassword(true)
      setOrigUsername(username.trim())
      setPassword('')
      setConfirmPassword('')
    } catch (err: any) {
      setMsg({ type: 'err', text: err?.response?.data?.message ?? 'บันทึกไม่สำเร็จ' })
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div
        className="bg-white dark:bg-gray-800 rounded-2xl shadow-xl w-full max-w-sm p-6"
        onClick={e => e.stopPropagation()}
      >
        <div className="flex items-center justify-between mb-5">
          <h2 className="text-lg font-bold text-gray-800 dark:text-white">โปรไฟล์ของฉัน</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl">✕</button>
        </div>

        {!loaded ? (
          <p className="text-sm text-gray-400 text-center py-10">กำลังโหลด...</p>
        ) : (
          <>
            <div className="flex items-center gap-3 mb-5">
              <div className="w-14 h-14 rounded-full bg-violet-200 flex items-center justify-center text-2xl overflow-hidden">
                {avatarUrl ? <img src={avatarUrl} alt="" className="w-full h-full object-cover" /> : '👤'}
              </div>
              <div>
                <p className="font-semibold text-gray-800 dark:text-white">{displayName || '—'}</p>
                <p className="text-xs text-gray-400">
                  {hasLine ? '🟢 ผูก LINE แล้ว' : '⚪ ยังไม่ได้ผูก LINE'}
                </p>
              </div>
            </div>

            <div className="space-y-3">
              <Field label="ชื่อที่แสดง" value={displayName} onChange={setDisplayName} placeholder="ชื่อที่แสดง" />
              <Field
                label="ชื่อผู้ใช้ (สำหรับ login)" value={username}
                onChange={v => { setUsername(v); clearInvalid('username') }}
                placeholder="เช่น owner01" autoComplete="username"
                hint="ตัวอักษร ตัวเลข _ เท่านั้น" error={invalid.has('username')}
              />
              <Field
                label={hasPassword ? 'เปลี่ยนรหัสผ่าน (ถ้าต้องการ)' : 'รหัสผ่านใหม่ *'} value={password}
                onChange={v => { setPassword(v); clearInvalid('password') }}
                placeholder="อย่างน้อย 6 ตัว" type="password" autoComplete="new-password"
                error={invalid.has('password')}
              />
              {password && (
                <Field
                  label="ยืนยันรหัสผ่าน" value={confirmPassword}
                  onChange={v => { setConfirmPassword(v); clearInvalid('confirm') }}
                  placeholder="กรอกรหัสผ่านอีกครั้ง" type="password" autoComplete="new-password"
                  error={invalid.has('confirm')}
                />
              )}
              <Field label="เบอร์โทร (ไว้รีเซ็ตรหัส)" value={phone} onChange={setPhone} placeholder="08xxxxxxxx" />
            </div>

            {msg && (
              <p className={`text-xs mt-3 text-center ${msg.type === 'ok' ? 'text-emerald-600' : 'text-red-500'}`}>
                {msg.text}
              </p>
            )}

            <button
              onClick={save}
              disabled={loading}
              className="w-full mt-5 py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white text-sm font-semibold rounded-lg disabled:opacity-50 transition"
            >
              {loading ? 'กำลังบันทึก...' : 'บันทึก'}
            </button>

            <button
              onClick={logout}
              className="w-full mt-2 py-2.5 text-red-500 text-sm font-medium hover:bg-red-50 dark:hover:bg-gray-700 rounded-lg transition"
            >
              ออกจากระบบ
            </button>
          </>
        )}
      </div>
    </div>
  )

  function clearInvalid(key: FieldKey) {
    if (invalid.has(key)) {
      const next = new Set(invalid)
      next.delete(key)
      setInvalid(next)
    }
  }
}

function Field({ label, value, onChange, placeholder, type = 'text', autoComplete, hint, error }: {
  label: string
  value: string
  onChange: (v: string) => void
  placeholder?: string
  type?: string
  autoComplete?: string
  hint?: string
  error?: boolean
}) {
  return (
    <div>
      <label className="block text-xs text-gray-500 dark:text-gray-400 mb-1">{label}</label>
      <input
        type={type}
        value={value}
        onChange={e => onChange(e.target.value)}
        placeholder={placeholder}
        autoComplete={autoComplete}
        className={`w-full text-sm border rounded-lg px-3 py-2 bg-white dark:bg-gray-700 text-gray-800 dark:text-white focus:outline-none focus:ring-2 transition
          ${error
            ? 'border-red-400 focus:ring-red-300 bg-red-50 dark:bg-red-900/20'
            : 'border-gray-300 dark:border-gray-600 focus:ring-emerald-300'}`}
      />
      {hint && <p className="text-xs text-gray-400 mt-1">{hint}</p>}
    </div>
  )
}
