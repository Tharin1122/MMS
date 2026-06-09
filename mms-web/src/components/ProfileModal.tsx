import { useEffect, useState } from 'react'
import { useAuthStore } from '../store/authStore'
import { api } from '../api/client'
import { LinkLineQRModal } from './LinkLineQRModal'

type FieldKey = 'username' | 'current' | 'password' | 'confirm'

export function ProfileModal({ onClose }: { onClose: () => void }) {
  const { logout, setUser } = useAuthStore()

  const [loaded, setLoaded] = useState(false)
  const [userId, setUserId] = useState('')
  const [avatarUrl, setAvatarUrl] = useState<string | null>(null)
  const [displayName, setDisplayName] = useState('')
  const [origDisplayName, setOrigDisplayName] = useState('')
  const [origPhone, setOrigPhone] = useState('')
  const [username, setUsername] = useState('')
  const [usernameLocked, setUsernameLocked] = useState(false)
  const [currentPassword, setCurrentPassword] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [phone, setPhone] = useState('')
  const [hasPassword, setHasPassword] = useState(false)
  const [hasLine, setHasLine] = useState(false)
  const [showLinkQR, setShowLinkQR] = useState(false)
  const [changingPw, setChangingPw] = useState(false)  // เปิดฟอร์มเปลี่ยนรหัส

  const [loading, setLoading] = useState(false)
  const [invalid, setInvalid] = useState<Set<FieldKey>>(new Set())
  const [msg, setMsg] = useState<{ type: 'ok' | 'err'; text: string } | null>(null)

  const fetchProfile = async () => {
    try {
      const res = await api.get('/auth/me')
      const d = res.data
      setUserId(d.userId ?? '')
      setDisplayName(d.displayName ?? '')
      setOrigDisplayName(d.displayName ?? '')
      setUsername(d.username ?? '')
      setUsernameLocked(!!d.username)
      setPhone(d.phone ?? '')
      setOrigPhone(d.phone ?? '')
      setAvatarUrl(d.avatarUrl ?? null)
      setHasPassword(!!d.hasPassword)
      setChangingPw(!d.hasPassword)
      setHasLine(!!d.hasLine)
      // อัปเดต nav avatar / ชื่อ ให้ตรง DB
      setUser({ displayName: d.displayName, avatarUrl: d.avatarUrl, hasLine: d.hasLine, username: d.username })
    } catch {
      setMsg({ type: 'err', text: 'โหลดข้อมูลโปรไฟล์ไม่สำเร็จ' })
    } finally {
      setLoaded(true)
    }
  }

  // ดึงข้อมูลโปรไฟล์สดจาก DB ทุกครั้งที่เปิด
  useEffect(() => { fetchProfile() }, [])

  const save = async () => {
    setMsg(null)
    const bad = new Set<FieldKey>()
    const settingUsername = !usernameLocked && username.trim() !== ''

    // ตัดสินใจว่ากำลัง "ตั้ง/เปลี่ยนรหัสผ่าน" อยู่ไหม → ถ้าใช่ ทุกช่องในฟอร์มเป็น mandatory
    const pwRequired = changingPw && (
      hasPassword                                   // กดเปลี่ยนรหัส (มีรหัสเดิมอยู่)
      || settingUsername                            // ตั้ง username ครั้งแรก ต้องมีรหัส
      || !!password || !!confirmPassword            // หรือเริ่มพิมพ์ในช่องรหัสแล้ว
    )

    // ── ตรวจทุกช่อง mandatory พร้อมกัน ──
    // Username
    if (settingUsername) {
      if (username.trim().length < 3 || !/^[a-zA-Z0-9_]+$/.test(username.trim()))
        bad.add('username')
    }
    // Password (ทั้งฟอร์มเป็น mandatory เมื่อ pwRequired)
    if (pwRequired) {
      if (hasPassword && !currentPassword.trim()) bad.add('current')
      if (!password) bad.add('password')
      else if (password.length < 6) bad.add('password')
      if (!confirmPassword) bad.add('confirm')
      else if (password && password !== confirmPassword) bad.add('confirm')
    }

    if (bad.size > 0) {
      setInvalid(bad)
      const labels: string[] = []
      if (bad.has('username')) labels.push('ชื่อผู้ใช้')
      if (bad.has('current')) labels.push('รหัสผ่านเดิม')
      if (bad.has('password')) labels.push('รหัสผ่านใหม่')
      if (bad.has('confirm')) labels.push('ยืนยันรหัสผ่าน')
      // ข้อความเฉพาะเจาะจงถ้ารหัสไม่ตรง
      const mismatch = password && confirmPassword && password !== confirmPassword
      setMsg({
        type: 'err',
        text: mismatch
          ? 'รหัสผ่านใหม่และยืนยันไม่ตรงกัน'
          : `กรุณากรอกให้ถูกต้อง: ${labels.join(', ')}`
      })
      return
    }

    setInvalid(new Set())
    setLoading(true)
    try {
      const body: Record<string, string> = {}
      if (displayName.trim()) body.displayName = displayName.trim()
      if (settingUsername) body.username = username.trim()
      if (changingPw && password) {
        body.password = password
        if (hasPassword) body.currentPassword = currentPassword
      }
      body.phone = phone.trim()

      await api.post('/auth/set-credentials', body)
      setMsg({ type: 'ok', text: 'บันทึกสำเร็จ' })
      if (password) { setHasPassword(true); setChangingPw(false) }
      if (settingUsername) setUsernameLocked(true)
      setOrigDisplayName(displayName.trim())
      setOrigPhone(phone.trim())
      setCurrentPassword('')
      setPassword('')
      setConfirmPassword('')
    } catch (err: any) {
      const text = err?.response?.data?.message ?? 'บันทึกไม่สำเร็จ'
      // ทำช่องที่เกี่ยวข้องเป็นสีแดงตาม error จาก server
      const serverBad = new Set<FieldKey>()
      if (text.includes('รหัสผ่านเดิม')) serverBad.add('current')
      if (text.includes('ชื่อผู้ใช้') || text.includes('มีคนใช้')) serverBad.add('username')
      if (serverBad.size > 0) setInvalid(serverBad)
      setMsg({ type: 'err', text })
    } finally {
      setLoading(false)
    }
  }

  // มีอะไรเปลี่ยนที่ต้องบันทึกไหม → ถ้าไม่มี ซ่อนปุ่มบันทึก
  const isDirty =
    displayName.trim() !== origDisplayName.trim() ||
    phone.trim() !== origPhone.trim() ||
    (!usernameLocked && username.trim() !== '') ||
    (changingPw && (!!password || !!confirmPassword || !!currentPassword))

  // ปิดด้วย backdrop ได้เฉพาะตอนไม่มีข้อมูลค้าง/ไม่ได้กำลังบันทึก (กันปิดโดยไม่ตั้งใจ)
  const handleBackdrop = () => {
    if (loading) return
    if (isDirty) {
      setMsg({ type: 'err', text: 'มีข้อมูลที่ยังไม่ได้บันทึก — กด ✕ เพื่อปิดโดยไม่บันทึก' })
      return
    }
    onClose()
  }

  return (
    <>
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={handleBackdrop}>
      <div
        className="bg-white dark:bg-gray-800 rounded-2xl shadow-xl w-full max-w-sm p-6 max-h-[90vh] overflow-y-auto"
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
            <div className="flex items-center gap-3 mb-4">
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

            {/* ผูก LINE */}
            <button
              onClick={() => setShowLinkQR(true)}
              className="w-full mb-5 py-2.5 border border-[#06C755] text-[#06C755] text-sm font-semibold rounded-lg hover:bg-green-50 dark:hover:bg-green-900/20 transition flex items-center justify-center gap-2"
            >
              <svg viewBox="0 0 24 24" className="w-5 h-5 fill-[#06C755]">
                <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm4.64 6.8c-.15 1.58-.8 5.42-1.13 7.19-.14.75-.42 1-.68 1.03-.58.05-1.02-.38-1.58-.75-.88-.58-1.38-.94-2.23-1.5-.99-.65-.35-1.01.22-1.59.15-.15 2.71-2.48 2.76-2.69.01-.03.01-.14-.07-.2-.08-.06-.19-.04-.27-.02-.12.02-1.96 1.25-5.54 3.69-.52.36-1 .53-1.42.52-.47-.01-1.37-.26-2.03-.48-.82-.27-1.47-.42-1.42-.88.03-.24.37-.49 1.02-.75 3.98-1.73 6.64-2.87 7.97-3.43 3.79-1.63 4.58-1.91 5.09-1.92.11 0 .37.03.53.17.14.12.18.28.2.46-.02.06-.02.12-.03.18z"/>
              </svg>
              {hasLine ? 'เปลี่ยน LINE ที่ผูก' : 'ผูกบัญชี LINE ของฉัน'}
            </button>

            <div className="space-y-3">
              <Field label="ชื่อที่แสดง" value={displayName} onChange={setDisplayName} placeholder="ชื่อที่แสดง" />

              <Field
                label="ชื่อผู้ใช้ (สำหรับ login)" value={username}
                onChange={v => { setUsername(v); clearInvalid('username') }}
                placeholder="เช่น owner01" autoComplete="username"
                disabled={usernameLocked}
                hint={usernameLocked ? '🔒 ตั้งแล้วเปลี่ยนไม่ได้' : 'ตัวอักษร ตัวเลข _ เท่านั้น'}
                error={invalid.has('username')}
              />

              {/* รหัสผ่าน */}
              {!changingPw ? (
                <button
                  onClick={() => setChangingPw(true)}
                  className="w-full py-2 border border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300 text-sm rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition"
                >
                  🔑 เปลี่ยนรหัสผ่าน
                </button>
              ) : (
                <div className="space-y-3 border border-gray-200 dark:border-gray-700 rounded-lg p-3">
                  <p className="text-xs font-semibold text-gray-600 dark:text-gray-300">
                    {hasPassword ? 'เปลี่ยนรหัสผ่าน' : 'ตั้งรหัสผ่านครั้งแรก'}
                  </p>
                  {hasPassword && (
                    <Field
                      label="รหัสผ่านเดิม *" value={currentPassword}
                      onChange={v => { setCurrentPassword(v); clearInvalid('current') }}
                      placeholder="รหัสผ่านปัจจุบัน" type="password" autoComplete="current-password"
                      error={invalid.has('current')}
                    />
                  )}
                  <Field
                    label={hasPassword ? 'รหัสผ่านใหม่ *' : 'รหัสผ่าน *'} value={password}
                    onChange={v => { setPassword(v); clearInvalid('password') }}
                    placeholder="อย่างน้อย 6 ตัว" type="password" autoComplete="new-password"
                    error={invalid.has('password')}
                  />
                  <Field
                    label="ยืนยันรหัสผ่าน *" value={confirmPassword}
                    onChange={v => { setConfirmPassword(v); clearInvalid('confirm') }}
                    placeholder="กรอกรหัสผ่านอีกครั้ง" type="password" autoComplete="new-password"
                    error={invalid.has('confirm')}
                  />
                  {hasPassword && (
                    <button
                      onClick={() => { setChangingPw(false); setCurrentPassword(''); setPassword(''); setConfirmPassword(''); clearAll() }}
                      className="text-xs text-gray-400 hover:text-gray-600"
                    >
                      ยกเลิกการเปลี่ยนรหัส
                    </button>
                  )}
                </div>
              )}

              <Field label="เบอร์โทร (ไว้รีเซ็ตรหัส)" value={phone} onChange={setPhone} placeholder="08xxxxxxxx" />
            </div>

            {msg && (
              <p className={`text-xs mt-3 text-center ${msg.type === 'ok' ? 'text-emerald-600' : 'text-red-500'}`}>
                {msg.text}
              </p>
            )}

            {isDirty && (
              <button
                onClick={save}
                disabled={loading}
                className="w-full mt-5 py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white text-sm font-semibold rounded-lg disabled:opacity-50 transition"
              >
                {loading ? 'กำลังบันทึก...' : 'บันทึก'}
              </button>
            )}

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

    {showLinkQR && userId && (
      <LinkLineQRModal
        userId={userId}
        userName={displayName}
        onClose={() => setShowLinkQR(false)}
        onLinked={() => { fetchProfile(); setMsg({ type: 'ok', text: '🟢 ผูก LINE สำเร็จ!' }) }}
      />
    )}
    </>
  )

  function clearInvalid(key: FieldKey) {
    if (invalid.has(key)) {
      const next = new Set(invalid)
      next.delete(key)
      setInvalid(next)
    }
  }
  function clearAll() {
    setInvalid(new Set())
    setMsg(null)
  }
}

function Field({ label, value, onChange, placeholder, type = 'text', autoComplete, hint, error, disabled }: {
  label: string
  value: string
  onChange: (v: string) => void
  placeholder?: string
  type?: string
  autoComplete?: string
  hint?: string
  error?: boolean
  disabled?: boolean
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
        disabled={disabled}
        className={`w-full text-sm border rounded-lg px-3 py-2 text-gray-800 dark:text-white focus:outline-none focus:ring-2 transition
          ${disabled ? 'bg-gray-100 dark:bg-gray-900 cursor-not-allowed text-gray-500' : 'bg-white dark:bg-gray-700'}
          ${error
            ? 'border-red-400 focus:ring-red-300 bg-red-50 dark:bg-red-900/20'
            : 'border-gray-300 dark:border-gray-600 focus:ring-emerald-300'}`}
      />
      {hint && <p className="text-xs text-gray-400 mt-1">{hint}</p>}
    </div>
  )
}
