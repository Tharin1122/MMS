import { useState } from 'react'
import { useAuthStore } from '../store/authStore'
import { api } from '../api/client'

export function ProfileModal({ onClose }: { onClose: () => void }) {
  const { user, logout } = useAuthStore()
  const [displayName, setDisplayName] = useState(user?.displayName ?? '')
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [phone, setPhone] = useState('')
  const [loading, setLoading] = useState(false)
  const [msg, setMsg] = useState<{ type: 'ok' | 'err'; text: string } | null>(null)

  const save = async () => {
    setLoading(true)
    setMsg(null)
    try {
      const body: Record<string, string> = {}
      if (displayName.trim()) body.displayName = displayName.trim()
      if (username.trim()) body.username = username.trim()
      if (password) body.password = password
      if (phone.trim()) body.phone = phone.trim()

      await api.post('/auth/set-credentials', body)
      setMsg({ type: 'ok', text: 'บันทึกสำเร็จ — ครั้งหน้าเข้าด้วยรหัสผ่านได้เลย' })
      setPassword('')
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

        <div className="flex items-center gap-3 mb-5">
          <div className="w-14 h-14 rounded-full bg-violet-200 flex items-center justify-center text-2xl overflow-hidden">
            {user?.avatarUrl ? <img src={user.avatarUrl} alt="" className="w-full h-full object-cover" /> : '👤'}
          </div>
          <div>
            <p className="font-semibold text-gray-800 dark:text-white">{user?.displayName}</p>
            <p className="text-xs text-gray-400">ตั้งรหัสผ่านเพื่อเข้าใช้แม้เปลี่ยนมือถือ/LINE</p>
          </div>
        </div>

        <div className="space-y-3">
          <Field label="ชื่อที่แสดง" value={displayName} onChange={setDisplayName} placeholder="ชื่อที่แสดง" />
          <Field label="ชื่อผู้ใช้ (สำหรับ login)" value={username} onChange={setUsername} placeholder="เช่น owner01" autoComplete="username" />
          <Field label="รหัสผ่านใหม่" value={password} onChange={setPassword} placeholder="อย่างน้อย 6 ตัว" type="password" autoComplete="new-password" />
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
      </div>
    </div>
  )
}

function Field({ label, value, onChange, placeholder, type = 'text', autoComplete }: {
  label: string
  value: string
  onChange: (v: string) => void
  placeholder?: string
  type?: string
  autoComplete?: string
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
        className="w-full text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 bg-white dark:bg-gray-700 text-gray-800 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-300"
      />
    </div>
  )
}
