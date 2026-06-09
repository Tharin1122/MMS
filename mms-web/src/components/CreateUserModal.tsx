import { useEffect, useState } from 'react'
import { api } from '../api/client'

interface Role { id: string; name: string; description: string }

const ROLE_LABEL: Record<string, string> = {
  Owner: 'เจ้าของร้าน', Manager: 'ผู้จัดการ', Reception: 'พนักงานต้อนรับ',
  Cashier: 'แคชเชียร์', Therapist: 'หมอนวด',
}

export function CreateUserModal({ onClose, onCreated }: { onClose: () => void; onCreated: (id: string, name: string) => void }) {
  const [roles, setRoles] = useState<Role[]>([])
  const [displayName, setDisplayName] = useState('')
  const [phone, setPhone] = useState('')
  const [roleId, setRoleId] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    api.get('/user/roles').then(res => {
      const list: Role[] = res.data.filter((r: Role) => r.name !== 'Owner') // ไม่ให้สร้าง Owner ง่ายๆ
      setRoles(list)
      const therapist = list.find(r => r.name === 'Therapist')
      setRoleId(therapist?.id ?? list[0]?.id ?? '')
    }).catch(() => setError('โหลดบทบาทไม่สำเร็จ'))
  }, [])

  const submit = async () => {
    if (!displayName.trim()) { setError('กรุณากรอกชื่อพนักงาน'); return }
    if (!roleId) { setError('กรุณาเลือกบทบาท'); return }
    setLoading(true); setError('')
    try {
      const res = await api.post('/user', { displayName: displayName.trim(), phone: phone.trim(), roleId })
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
            <select value={roleId} onChange={e => setRoleId(e.target.value)}
              className="w-full text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 bg-white dark:bg-gray-700 dark:text-white">
              {roles.map(r => <option key={r.id} value={r.id}>{ROLE_LABEL[r.name] ?? r.name}</option>)}
            </select>
          </div>
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
