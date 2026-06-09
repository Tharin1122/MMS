import { useEffect, useState } from 'react'
import { api } from '../api/client'

interface Role { id: string; name: string; description: string }

const ROLE_LABEL: Record<string, string> = {
  Owner: 'เจ้าของร้าน', Manager: 'ผู้จัดการ', Reception: 'พนักงานต้อนรับ',
  Cashier: 'แคชเชียร์', Therapist: 'หมอนวด',
}

export function CreateUserModal({ onClose, onCreated }: { onClose: () => void; onCreated: (id: string, name: string) => void }) {
  const [roles, setRoles] = useState<Role[]>([])
  const [rolesLoading, setRolesLoading] = useState(true)
  const [displayName, setDisplayName] = useState('')
  const [phone, setPhone] = useState('')
  const [roleId, setRoleId] = useState('')
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [showLogin, setShowLogin] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    api.get('/user/roles')
      .then(res => {
        const list: Role[] = (res.data ?? []).filter((r: Role) => r.name !== 'Owner')
        setRoles(list)
        const therapist = list.find(r => r.name === 'Therapist')
        setRoleId(therapist?.id ?? list[0]?.id ?? '')
      })
      .catch(() => setError('โหลดบทบาทไม่สำเร็จ'))
      .finally(() => setRolesLoading(false))
  }, [])

  const submit = async () => {
    if (!displayName.trim()) { setError('กรุณากรอกชื่อพนักงาน'); return }
    if (!roleId) { setError('กรุณาเลือกบทบาท'); return }
    if (showLogin && username.trim()) {
      if (username.trim().length < 3 || !/^[a-zA-Z0-9_]+$/.test(username.trim())) {
        setError('ชื่อผู้ใช้: อย่างน้อย 3 ตัว ใช้ตัวอักษร ตัวเลข _ เท่านั้น'); return
      }
      if (password && password.length < 6) { setError('รหัสผ่านอย่างน้อย 6 ตัว'); return }
    }
    setLoading(true); setError('')
    try {
      const body: any = { displayName: displayName.trim(), phone: phone.trim(), roleId }
      if (showLogin && username.trim()) { body.username = username.trim(); if (password) body.password = password }
      const res = await api.post('/user', body)
      onCreated(res.data.id, res.data.displayName)
    } catch (err: any) {
      setError(err?.response?.data?.message ?? 'เพิ่มพนักงานไม่สำเร็จ')
    } finally { setLoading(false) }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-xl w-full max-w-sm p-6" onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between mb-5">
          <h2 className="text-lg font-bold text-gray-800 dark:text-white">เพิ่มพนักงาน</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl">✕</button>
        </div>

        <div className="space-y-3">
          <div>
            <label className="block text-xs text-gray-500 dark:text-gray-400 mb-1">ชื่อพนักงาน *</label>
            <input value={displayName} onChange={e => setDisplayName(e.target.value)} placeholder="เช่น มิ้นท์"
              className="w-full text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-300" />
          </div>
          <div>
            <label className="block text-xs text-gray-500 dark:text-gray-400 mb-1">เบอร์โทร</label>
            <input value={phone} onChange={e => setPhone(e.target.value)} placeholder="08xxxxxxxx"
              className="w-full text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-300" />
          </div>
          <div>
            <label className="block text-xs text-gray-500 dark:text-gray-400 mb-1">บทบาท *</label>
            {rolesLoading ? (
              <p className="text-sm text-gray-400 py-2">กำลังโหลดบทบาท...</p>
            ) : roles.length === 0 ? (
              <p className="text-sm text-red-500 py-2">ไม่พบบทบาทในระบบ กรุณาติดต่อผู้ดูแล</p>
            ) : (
              <select value={roleId} onChange={e => setRoleId(e.target.value)}
                className="w-full text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 bg-white dark:bg-gray-700 dark:text-white">
                {roles.map(r => <option key={r.id} value={r.id}>{ROLE_LABEL[r.name] ?? r.name}</option>)}
              </select>
            )}
          </div>
        </div>

          {/* ตั้ง login user/pass ทันที (ออปชัน) */}
          <div className="border-t border-gray-200 dark:border-gray-700 pt-3">
            {!showLogin ? (
              <button onClick={() => setShowLogin(true)}
                className="text-xs text-violet-600 dark:text-violet-400 hover:underline">
                + ตั้งชื่อผู้ใช้/รหัสผ่านให้เลย (ให้ login ได้ทันทีไม่ต้องรอ LINE)
              </button>
            ) : (
              <div className="space-y-3">
                <div>
                  <label className="block text-xs text-gray-500 dark:text-gray-400 mb-1">ชื่อผู้ใช้ (login)</label>
                  <input value={username} onChange={e => setUsername(e.target.value)} placeholder="เช่น mint01" autoComplete="off"
                    className="w-full text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-300" />
                </div>
                <div>
                  <label className="block text-xs text-gray-500 dark:text-gray-400 mb-1">รหัสผ่านชั่วคราว</label>
                  <input value={password} onChange={e => setPassword(e.target.value)} placeholder="อย่างน้อย 6 ตัว" autoComplete="off"
                    className="w-full text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-300" />
                  <p className="text-xs text-gray-400 mt-1">บอกพนักงานให้เปลี่ยนรหัสเองภายหลัง</p>
                </div>
              </div>
            )}
          </div>

        {error && <p className="text-red-500 text-xs mt-3 text-center">{error}</p>}

        <button onClick={submit} disabled={loading}
          className="w-full mt-5 py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white text-sm font-semibold rounded-lg disabled:opacity-50 transition">
          {loading ? 'กำลังเพิ่ม...' : 'เพิ่มพนักงาน'}
        </button>
        <p className="text-xs text-gray-400 text-center mt-2">หลังเพิ่มแล้ว สร้าง QR ให้พนักงานสแกนผูก LINE ได้เลย</p>
      </div>
    </div>
  )
}
