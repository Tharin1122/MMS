import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { useAuthStore } from '../store/authStore'

interface Customer {
  id: string; displayName: string; phone?: string; avatarUrl?: string
  totalVisits: number; totalSpent: number; lastVisitAt?: string; notes?: string
}

const baht = (n: number) => (n ?? 0).toLocaleString('th-TH')

export default function CustomerPage() {
  const perms = useAuthStore(s => s.permissions)
  const canEdit = perms.includes('CUSTOMER_CREATE') || perms.includes('CUSTOMER_EDIT')

  const [items, setItems] = useState<Customer[]>([])
  const [total, setTotal] = useState(0)
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [editing, setEditing] = useState<Customer | null>(null)
  const [showForm, setShowForm] = useState(false)

  const load = async (q = '') => {
    setLoading(true)
    try {
      const res = await api.get(`/customer?search=${encodeURIComponent(q)}&pageSize=50`)
      setItems(res.data.items); setTotal(res.data.total)
    } catch { /* */ } finally { setLoading(false) }
  }
  useEffect(() => { load() }, [])

  useEffect(() => {
    const t = setTimeout(() => load(search), 350)  // debounce search
    return () => clearTimeout(t)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search])

  const del = async (id: string) => {
    if (!confirm('ยืนยันลบลูกค้านี้?')) return
    try { await api.delete(`/customer/${id}`); load(search) } catch (e: any) { alert(e?.response?.data?.message ?? 'ลบไม่สำเร็จ') }
  }

  return (
    <div className="p-4 space-y-4">
      <div className="flex items-center justify-between flex-wrap gap-2">
        <h1 className="text-lg font-bold dark:text-white">👤 ลูกค้า <span className="text-sm font-normal text-gray-400">({total})</span></h1>
        {canEdit && (
          <button onClick={() => { setEditing(null); setShowForm(true) }}
            className="px-3 py-1.5 bg-violet-600 hover:bg-violet-700 text-white text-sm rounded-lg transition">+ เพิ่มลูกค้า</button>
        )}
      </div>

      <input value={search} onChange={e => setSearch(e.target.value)} placeholder="🔍 ค้นหาชื่อหรือเบอร์โทร..."
        className="w-full text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-violet-300" />

      {loading && <p className="text-sm text-gray-400 text-center py-10">กำลังโหลด...</p>}
      {!loading && items.length === 0 && (
        <div className="bg-white dark:bg-gray-800 rounded-xl p-8 text-center">
          <p className="text-4xl mb-2">👥</p>
          <p className="text-sm text-gray-500 dark:text-gray-400">{search ? 'ไม่พบลูกค้า' : 'ยังไม่มีลูกค้า'}</p>
        </div>
      )}

      {!loading && items.length > 0 && (
        <div className="bg-white dark:bg-gray-800 rounded-xl divide-y dark:divide-gray-700">
          {items.map(c => (
            <div key={c.id} className="flex items-center justify-between px-4 py-3">
              <div className="flex items-center gap-3 min-w-0">
                <div className="w-9 h-9 rounded-full bg-violet-100 flex items-center justify-center text-violet-700 text-sm font-medium overflow-hidden flex-shrink-0">
                  {c.avatarUrl ? <img src={c.avatarUrl} alt="" className="w-full h-full object-cover" /> : c.displayName.charAt(0).toUpperCase()}
                </div>
                <div className="min-w-0">
                  <p className="text-sm font-medium dark:text-white truncate">{c.displayName}</p>
                  <p className="text-xs text-gray-400 truncate">
                    {c.phone || 'ไม่มีเบอร์'} · มา {c.totalVisits} ครั้ง · ฿{baht(c.totalSpent)}
                  </p>
                </div>
              </div>
              {canEdit && (
                <div className="flex items-center gap-2 flex-shrink-0">
                  <button onClick={() => { setEditing(c); setShowForm(true) }} className="text-xs text-emerald-600 border border-emerald-300 px-2.5 py-1 rounded-lg">แก้ไข</button>
                  <button onClick={() => del(c.id)} className="text-xs text-red-500 border border-red-200 px-2.5 py-1 rounded-lg">ลบ</button>
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      {showForm && (
        <CustomerForm customer={editing} onClose={() => setShowForm(false)} onSaved={() => { setShowForm(false); load(search) }} />
      )}
    </div>
  )
}

function CustomerForm({ customer, onClose, onSaved }: { customer: Customer | null; onClose: () => void; onSaved: () => void }) {
  const [displayName, setDisplayName] = useState(customer?.displayName ?? '')
  const [phone, setPhone] = useState(customer?.phone ?? '')
  const [notes, setNotes] = useState(customer?.notes ?? '')
  const [loading, setLoading] = useState(false)
  const [err, setErr] = useState('')

  const save = async () => {
    if (!displayName.trim()) { setErr('กรอกชื่อลูกค้า'); return }
    setLoading(true); setErr('')
    try {
      const body = { displayName: displayName.trim(), phone: phone.trim() || null, notes: notes.trim() || null }
      if (customer) await api.put(`/customer/${customer.id}`, body)
      else await api.post('/customer', body)
      onSaved()
    } catch (e: any) { setErr(e?.response?.data?.message ?? 'บันทึกไม่สำเร็จ') }
    finally { setLoading(false) }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-xl w-full max-w-sm p-6" onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between mb-5">
          <h2 className="text-lg font-bold text-gray-800 dark:text-white">{customer ? 'แก้ไขลูกค้า' : 'เพิ่มลูกค้า'}</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl">✕</button>
        </div>
        <div className="space-y-3">
          <div>
            <label className="block text-xs text-gray-500 dark:text-gray-400 mb-1">ชื่อลูกค้า *</label>
            <input value={displayName} onChange={e => setDisplayName(e.target.value)} placeholder="ชื่อ-นามสกุล"
              className="w-full text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-violet-300" />
          </div>
          <div>
            <label className="block text-xs text-gray-500 dark:text-gray-400 mb-1">เบอร์โทร</label>
            <input value={phone} onChange={e => setPhone(e.target.value)} placeholder="08xxxxxxxx"
              className="w-full text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-violet-300" />
          </div>
          <div>
            <label className="block text-xs text-gray-500 dark:text-gray-400 mb-1">โน้ต (ความชอบ/แพ้)</label>
            <textarea value={notes} onChange={e => setNotes(e.target.value)} placeholder="เช่น ชอบนวดหนัก, แพ้น้ำมันบางชนิด" rows={2}
              className="w-full text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-violet-300" />
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
