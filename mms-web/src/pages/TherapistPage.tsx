import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { useAuthStore } from '../store/authStore'

interface Therapist {
  id: string; code?: string; displayName: string; phone?: string; avatarUrl?: string
  skillLevel?: number; experienceYears?: number; currentStatus: number; isActive: boolean
}

const STATUS = ['ว่าง', 'ไม่ว่าง', 'พัก', 'ลา', 'เลิกงาน', 'ออฟไลน์']
const STATUS_COLOR = ['bg-emerald-100 text-emerald-700', 'bg-red-100 text-red-700', 'bg-amber-100 text-amber-700', 'bg-purple-100 text-purple-700', 'bg-gray-200 text-gray-600', 'bg-gray-100 text-gray-400']
const SKILL = ['จูเนียร์', 'ซีเนียร์', 'เชี่ยวชาญ']

export default function TherapistPage() {
  const perms = useAuthStore(s => s.permissions)
  const canEdit = perms.includes('THERAPIST_CREATE') || perms.includes('THERAPIST_EDIT')
  const canStatus = perms.includes('THERAPIST_STATUS_CHANGE')

  const [items, setItems] = useState<Therapist[]>([])
  const [loading, setLoading] = useState(true)
  const [editing, setEditing] = useState<Therapist | null>(null)
  const [showForm, setShowForm] = useState(false)

  const load = async () => {
    setLoading(true)
    try { setItems((await api.get('/therapist')).data) } catch { /* */ } finally { setLoading(false) }
  }
  useEffect(() => { load() }, [])

  const del = async (id: string) => {
    if (!confirm('ยืนยันลบหมอนวดคนนี้?')) return
    try { await api.delete(`/therapist/${id}`); load() } catch (e: any) { alert(e?.response?.data?.message ?? 'ลบไม่สำเร็จ') }
  }
  const changeStatus = async (id: string, status: number) => {
    try { await api.patch(`/therapist/${id}/status`, { status }); load() } catch (e: any) { alert(e?.response?.data?.message ?? 'เปลี่ยนสถานะไม่สำเร็จ') }
  }

  return (
    <div className="p-4 space-y-4">
      <div className="flex items-center justify-between flex-wrap gap-2">
        <h1 className="text-lg font-bold dark:text-white">🧑‍⚕️ หมอนวด / พนักงาน <span className="text-sm font-normal text-gray-400">({items.length})</span></h1>
        {canEdit && (
          <button onClick={() => { setEditing(null); setShowForm(true) }}
            className="px-3 py-1.5 bg-violet-600 hover:bg-violet-700 text-white text-sm rounded-lg transition">+ เพิ่มหมอนวด</button>
        )}
      </div>

      {loading && <p className="text-sm text-gray-400 text-center py-10">กำลังโหลด...</p>}
      {!loading && items.length === 0 && (
        <div className="bg-white dark:bg-gray-800 rounded-xl p-8 text-center">
          <p className="text-4xl mb-2">🧑‍⚕️</p>
          <p className="text-sm text-gray-500 dark:text-gray-400">ยังไม่มีหมอนวด</p>
          <p className="text-xs text-gray-400 mt-1">เพิ่มหมอนวดเพื่อจัดคิวและตารางงาน</p>
        </div>
      )}

      {!loading && items.length > 0 && (
        <div className="bg-white dark:bg-gray-800 rounded-xl divide-y dark:divide-gray-700">
          {items.map(t => (
            <div key={t.id} className="flex items-center justify-between px-4 py-3 gap-2">
              <div className="flex items-center gap-3 min-w-0">
                <div className="w-9 h-9 rounded-full bg-emerald-100 flex items-center justify-center text-emerald-700 text-sm font-medium overflow-hidden flex-shrink-0">
                  {t.avatarUrl ? <img src={t.avatarUrl} alt="" className="w-full h-full object-cover" /> : t.displayName.charAt(0).toUpperCase()}
                </div>
                <div className="min-w-0">
                  <p className="text-sm font-medium dark:text-white truncate flex items-center gap-2">
                    {t.displayName}
                    {t.code && <span className="text-xs text-gray-400">#{t.code}</span>}
                  </p>
                  <p className="text-xs text-gray-400">
                    {t.skillLevel != null ? SKILL[t.skillLevel] : '—'}
                    {t.experienceYears ? ` · ${t.experienceYears} ปี` : ''}
                  </p>
                </div>
              </div>
              <div className="flex items-center gap-2 flex-shrink-0">
                {canStatus ? (
                  <select value={t.currentStatus} onChange={e => changeStatus(t.id, Number(e.target.value))}
                    className={`text-xs rounded-lg px-2 py-1 border-0 ${STATUS_COLOR[t.currentStatus] ?? ''}`}>
                    {STATUS.map((s, i) => <option key={i} value={i}>{s}</option>)}
                  </select>
                ) : (
                  <span className={`text-xs px-2 py-1 rounded-lg ${STATUS_COLOR[t.currentStatus] ?? ''}`}>{STATUS[t.currentStatus]}</span>
                )}
                {canEdit && (
                  <>
                    <button onClick={() => { setEditing(t); setShowForm(true) }} className="text-xs text-emerald-600 border border-emerald-300 px-2.5 py-1 rounded-lg">แก้ไข</button>
                    <button onClick={() => del(t.id)} className="text-xs text-red-500 border border-red-200 px-2.5 py-1 rounded-lg">ลบ</button>
                  </>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {showForm && (
        <TherapistForm therapist={editing} onClose={() => setShowForm(false)} onSaved={() => { setShowForm(false); load() }} />
      )}
    </div>
  )
}

interface UserOpt { id: string; displayName: string; hasLine: boolean }

function TherapistForm({ therapist, onClose, onSaved }: { therapist: Therapist | null; onClose: () => void; onSaved: () => void }) {
  const [fromUser, setFromUser] = useState(false)
  const [users, setUsers] = useState<UserOpt[]>([])
  const [selectedUserId, setSelectedUserId] = useState('')
  const [displayName, setDisplayName] = useState(therapist?.displayName ?? '')
  const [code, setCode] = useState(therapist?.code ?? '')
  const [phone, setPhone] = useState(therapist?.phone ?? '')
  const [exp, setExp] = useState(String(therapist?.experienceYears ?? ''))
  const [skill, setSkill] = useState(String(therapist?.skillLevel ?? 0))
  const [loading, setLoading] = useState(false)
  const [err, setErr] = useState('')

  const enableFromUser = async () => {
    setFromUser(true)
    if (users.length === 0) {
      try { setUsers((await api.get('/user')).data) } catch { setErr('โหลดรายชื่อผู้ใช้ไม่สำเร็จ') }
    }
  }

  const save = async () => {
    setLoading(true); setErr('')
    try {
      if (fromUser && !therapist) {
        if (!selectedUserId) { setErr('เลือกผู้ใช้'); setLoading(false); return }
        await api.post('/therapist/from-user', {
          userId: selectedUserId, code: code.trim() || null,
          experienceYears: exp ? Number(exp) : null, skillLevel: Number(skill),
        })
      } else {
        if (!displayName.trim()) { setErr('กรอกชื่อหมอนวด'); setLoading(false); return }
        const body = {
          displayName: displayName.trim(), code: code.trim() || null, phone: phone.trim() || null,
          experienceYears: exp ? Number(exp) : null, skillLevel: Number(skill),
        }
        if (therapist) await api.put(`/therapist/${therapist.id}`, body)
        else await api.post('/therapist', body)
      }
      onSaved()
    } catch (e: any) { setErr(e?.response?.data?.message ?? 'บันทึกไม่สำเร็จ') }
    finally { setLoading(false) }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-xl w-full max-w-sm p-6" onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between mb-5">
          <h2 className="text-lg font-bold text-gray-800 dark:text-white">{therapist ? 'แก้ไขหมอนวด' : 'เพิ่มหมอนวด'}</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl">✕</button>
        </div>
        <div className="space-y-3">
          {!therapist && (
            <div className="bg-violet-50 dark:bg-violet-900/20 rounded-lg p-2.5">
              {!fromUser ? (
                <button onClick={enableFromUser} className="text-xs text-violet-600 dark:text-violet-300 hover:underline">
                  👥 ดึงจากผู้ใช้ในระบบ (หมอนวดที่มี account login อยู่แล้ว)
                </button>
              ) : (
                <div>
                  <label className="block text-xs text-gray-500 dark:text-gray-400 mb-1">เลือกผู้ใช้ *</label>
                  <select value={selectedUserId} onChange={e => setSelectedUserId(e.target.value)}
                    className="w-full text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 bg-white dark:bg-gray-700 dark:text-white">
                    <option value="">— เลือกผู้ใช้ —</option>
                    {users.map(u => <option key={u.id} value={u.id}>{u.displayName}{u.hasLine ? ' 🟢' : ''}</option>)}
                  </select>
                  <button onClick={() => { setFromUser(false); setSelectedUserId('') }} className="text-xs text-gray-400 mt-1 hover:text-gray-600">← กรอกเองแทน</button>
                </div>
              )}
            </div>
          )}
          {!fromUser && <F label="ชื่อหมอนวด *" value={displayName} onChange={setDisplayName} placeholder="เช่น มิ้นท์" />}
          <div className="grid grid-cols-2 gap-3">
            <F label="รหัส" value={code} onChange={setCode} placeholder="เช่น T01" />
            <F label="เบอร์โทร" value={phone} onChange={setPhone} placeholder="08xxxxxxxx" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <F label="ประสบการณ์ (ปี)" value={exp} onChange={setExp} type="number" />
            <div>
              <label className="block text-xs text-gray-500 dark:text-gray-400 mb-1">ระดับ</label>
              <select value={skill} onChange={e => setSkill(e.target.value)}
                className="w-full text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 bg-white dark:bg-gray-700 dark:text-white">
                <option value="0">จูเนียร์</option><option value="1">ซีเนียร์</option><option value="2">เชี่ยวชาญ</option>
              </select>
            </div>
          </div>
        </div>
        {err && <p className="text-red-500 text-xs mt-3 text-center">{err}</p>}
        <button onClick={save} disabled={loading} className="w-full mt-5 py-2.5 bg-violet-600 hover:bg-violet-700 text-white text-sm font-semibold rounded-lg disabled:opacity-50 transition">
          {loading ? 'กำลังบันทึก...' : 'บันทึก'}
        </button>
      </div>
    </div>
  )
}

function F({ label, value, onChange, placeholder, type = 'text' }: {
  label: string; value: string; onChange: (v: string) => void; placeholder?: string; type?: string
}) {
  return (
    <div>
      <label className="block text-xs text-gray-500 dark:text-gray-400 mb-1">{label}</label>
      <input type={type} value={value} onChange={e => onChange(e.target.value)} placeholder={placeholder}
        className="w-full text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-violet-300" />
    </div>
  )
}
