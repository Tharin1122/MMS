import { useState } from 'react'
import { api } from '../api/client'

type Step = 'username' | 'reset'
type FieldKey = 'username' | 'otp' | 'password' | 'confirm'

export function ForgotPasswordModal({ onClose, onDone }: { onClose: () => void; onDone: () => void }) {
  const [step, setStep] = useState<Step>('username')
  const [username, setUsername] = useState('')
  const [otp, setOtp] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [invalid, setInvalid] = useState<Set<FieldKey>>(new Set())
  const [msg, setMsg] = useState<{ type: 'ok' | 'err'; text: string } | null>(null)

  // ขั้นที่ 1 — ขอ OTP
  const requestOtp = async () => {
    setMsg(null)
    if (!username.trim()) {
      setInvalid(new Set(['username']))
      setMsg({ type: 'err', text: 'กรุณากรอกชื่อผู้ใช้' })
      return
    }
    setLoading(true)
    try {
      await api.post('/auth/request-reset-otp', { username: username.trim() })
      setMsg({ type: 'ok', text: '✅ ส่ง OTP ไปยัง LINE ของคุณแล้ว (ใช้ได้ 10 นาที)' })
      setInvalid(new Set())
      setStep('reset')
    } catch (err: any) {
      setMsg({ type: 'err', text: err?.response?.data?.message ?? 'ส่ง OTP ไม่สำเร็จ' })
      setInvalid(new Set(['username']))
    } finally {
      setLoading(false)
    }
  }

  // ขั้นที่ 2 — ยืนยัน OTP + ตั้งรหัสใหม่
  const resetPassword = async () => {
    setMsg(null)
    const bad = new Set<FieldKey>()
    if (!otp.trim()) bad.add('otp')
    if (!password) bad.add('password')
    else if (password.length < 6) bad.add('password')
    if (!confirmPassword) bad.add('confirm')
    else if (password && password !== confirmPassword) bad.add('confirm')

    if (bad.size > 0) {
      setInvalid(bad)
      const mismatch = password && confirmPassword && password !== confirmPassword
      setMsg({ type: 'err', text: mismatch ? 'รหัสผ่านใหม่ไม่ตรงกัน' : 'กรุณากรอกข้อมูลให้ครบและถูกต้อง' })
      return
    }

    setInvalid(new Set())
    setLoading(true)
    try {
      await api.post('/auth/reset-password', {
        username: username.trim(),
        otp: otp.trim(),
        newPassword: password,
      })
      setMsg({ type: 'ok', text: '✅ รีเซ็ตรหัสผ่านสำเร็จ! กำลังกลับไปหน้าเข้าสู่ระบบ...' })
      setTimeout(onDone, 1500)
    } catch (err: any) {
      const text = err?.response?.data?.message ?? 'รีเซ็ตรหัสผ่านไม่สำเร็จ'
      setMsg({ type: 'err', text })
      if (text.includes('OTP')) setInvalid(new Set(['otp']))
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
          <h2 className="text-lg font-bold text-gray-800 dark:text-white">ลืมรหัสผ่าน</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl">✕</button>
        </div>

        {step === 'username' ? (
          <div className="space-y-3">
            <p className="text-xs text-gray-500 dark:text-gray-400">
              กรอกชื่อผู้ใช้ ระบบจะส่งรหัส OTP ไปยัง LINE ที่ผูกไว้
            </p>
            <Field
              label="ชื่อผู้ใช้" value={username}
              onChange={v => { setUsername(v); clearInvalid('username') }}
              placeholder="ชื่อผู้ใช้ของคุณ" autoComplete="username"
              error={invalid.has('username')}
            />
            <button
              onClick={requestOtp} disabled={loading}
              className="w-full py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white text-sm font-semibold rounded-lg disabled:opacity-50 transition"
            >
              {loading ? 'กำลังส่ง...' : 'ส่ง OTP ไปยัง LINE'}
            </button>
          </div>
        ) : (
          <div className="space-y-3">
            <p className="text-xs text-gray-500 dark:text-gray-400">
              กรอกรหัส OTP ที่ได้รับใน LINE แล้วตั้งรหัสผ่านใหม่
            </p>
            <Field
              label="รหัส OTP (6 หลัก)" value={otp}
              onChange={v => { setOtp(v.replace(/\D/g, '').slice(0, 6)); clearInvalid('otp') }}
              placeholder="••••••" inputMode="numeric"
              error={invalid.has('otp')}
            />
            <Field
              label="รหัสผ่านใหม่ *" value={password}
              onChange={v => { setPassword(v); clearInvalid('password') }}
              placeholder="อย่างน้อย 6 ตัว" type="password" autoComplete="new-password"
              error={invalid.has('password')}
            />
            <Field
              label="ยืนยันรหัสผ่านใหม่ *" value={confirmPassword}
              onChange={v => { setConfirmPassword(v); clearInvalid('confirm') }}
              placeholder="กรอกรหัสผ่านอีกครั้ง" type="password" autoComplete="new-password"
              error={invalid.has('confirm')}
            />
            <button
              onClick={resetPassword} disabled={loading}
              className="w-full py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white text-sm font-semibold rounded-lg disabled:opacity-50 transition"
            >
              {loading ? 'กำลังรีเซ็ต...' : 'ยืนยันรีเซ็ตรหัสผ่าน'}
            </button>
            <button
              onClick={() => { setStep('username'); setMsg(null); setInvalid(new Set()) }}
              className="w-full text-xs text-gray-400 hover:text-gray-600"
            >
              ← ขอ OTP ใหม่
            </button>
          </div>
        )}

        {msg && (
          <p className={`text-xs mt-3 text-center ${msg.type === 'ok' ? 'text-emerald-600' : 'text-red-500'}`}>
            {msg.text}
          </p>
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

function Field({ label, value, onChange, placeholder, type = 'text', autoComplete, inputMode, error }: {
  label: string
  value: string
  onChange: (v: string) => void
  placeholder?: string
  type?: string
  autoComplete?: string
  inputMode?: 'numeric' | 'text'
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
        inputMode={inputMode}
        className={`w-full text-sm border rounded-lg px-3 py-2 bg-white dark:bg-gray-700 text-gray-800 dark:text-white focus:outline-none focus:ring-2 transition
          ${error
            ? 'border-red-400 focus:ring-red-300 bg-red-50 dark:bg-red-900/20'
            : 'border-gray-300 dark:border-gray-600 focus:ring-emerald-300'}`}
      />
    </div>
  )
}
