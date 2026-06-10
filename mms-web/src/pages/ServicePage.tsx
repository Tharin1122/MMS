import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { useAuthStore } from '../store/authStore'

interface Category { id: string; name: string; sortOrder: number; isActive: boolean }
interface Service {
  id: string; name: string; durationMins: number; bufferMins: number
  price: number; commissionRate?: number; commissionFixed?: number
  requiredRoomType?: string; isActive: boolean
  category: { id: string; name: string }
}

const baht = (n: number) => n.toLocaleString('th-TH')

export default function ServicePage() {
  const branchId = useAuthStore(s => s.user?.branchId)
  const perms = useAuthStore(s => s.permissions)
  const canEdit = perms.includes('SERVICE_CREATE') || perms.includes('SERVICE_EDIT')

  const [cats, setCats] = useState<Category[]>([])
  const [services, setServices] = useState<Service[]>([])
  const [loading, setLoading] = useState(true)
  const [editing, setEditing] = useState<Service | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [showCatForm, setShowCatForm] = useState(false)
  const [msg, setMsg] = useState('')

  const load = async () => {
    setLoading(true)
    try {
      const [c, s] = await Promise.all([api.get('/service-categories'), api.get('/services')])
      setCats(c.data); setServices(s.data)
    } catch { setMsg('โหลดข้อมูลไม่สำเร็จ') }
    finally { setLoading(false) }
  }
  useEffect(() => { load() }, [])

  const del = async (id: string) => {
    if (!confirm('ยืนยันลบบริการนี้?')) return
    try { await api.delete(`/services/${id}`); load() } catch (e: any) { alert(e?.response?.data?.message ?? 'ลบไม่สำเร็จ') }
  }

  const grouped = cats.map(c => ({ cat: c, items: services.filter(s => s.category.id === c.id) }))
  const uncategorized = services.filter(s => !cats.some(c => c.id === s.category.id))

  return (
    <div className="p-4 space-y-4">
      <div className="flex items-center justify-between flex-wrap gap-2">
        <h1 className="text-lg font-bold dark:text-white">💆 บริการ & คอร์ส <span className="text-sm font-normal text-gray-400">({services.length})</span></h1>
        {canEdit && (
          <div className="flex gap-2">
            <button onClick={() => setShowCatForm(true)} className="px-3 py-1.5 border border-violet-300 text-violet-600 text-sm rounded-lg hover:bg-violet-50 dark:hover:bg-violet-900/20 transition">+ หมวดหมู่</button>
            <button onClick={() => { setEditing(null); setShowForm(true) }} disabled={cats.length === 0}
              className="px-3 py-1.5 bg-violet-600 hover:bg-violet-700 text-white text-sm rounded-lg disabled:opacity-50 transition">+ เพิ่มบริการ</button>
          </div>
        )}
      </div>

      {loading && <p className="text-sm text-gray-400 text-center py-10">กำลังโหลด...</p>}
      {!loading && cats.length === 0 && (
        <div className="bg-white dark:bg-gray-800 rounded-xl p-8 text-center">
          <p className="text-4xl mb-2">📋</p>
          <p className="text-sm text-gray-500 dark:text-gray-400">ยังไม่มีหมวดหมู่บริการ</p>
          <p className="text-xs text-gray-400 mt-1">เริ่มจากเพิ่มหมวดหมู่ก่อน เช่น "นวดไทย", "นวดน้ำมัน"</p>
        </div>
      )}

      {!loading && [...grouped, ...(uncategorized.length ? [{ cat: { id: 'none', name: 'ไม่มีหมวดหมู่', sortOrder: 999, isActive: true }, items: uncategorized }] : [])].map(({ cat, items }) => (
        items.length > 0 || cat.id !== 'none' ? (
          <div key={cat.id}>
            <h2 className="text-sm font-semibold text-gray-500 dark:text-gray-400 mb-2">{cat.name} <span className="text-gray-400">({items.length})</span></h2>
            <div className="bg-white dark:bg-gray-800 rounded-xl divide-y dark:divide-gray-700">
              {items.length === 0 ? (
                <p className="text-xs text-gray-400 px-4 py-3">ยังไม่มีบริการในหมวดนี้</p>
              ) : items.map(s => (
                <div key={s.id} className="flex items-center justify-between px-4 py-3">
                  <div className="min-w-0">
                    <p className="text-sm font-medium dark:text-white truncate flex items-center gap-2">
                      {s.name}
                      {!s.isActive && <span className="text-xs bg-gray-200 dark:bg-gray-600 text-gray-500 px-1.5 rounded">ปิด</span>}
                    </p>
                    <p className="text-xs text-gray-400">{s.durationMins} นาที · ฿{baht(s.price)}</p>
                  </div>
                  {canEdit && (
                    <div className="flex items-center gap-2 flex-shrink-0">
                      <button onClick={() => { setEditing(s); setShowForm(true) }} className="text-xs text-emerald-600 border border-emerald-300 px-2.5 py-1 rounded-lg">แก้ไข</button>
                      <button onClick={() => del(s.id)} className="text-xs text-red-500 border border-red-200 px-2.5 py-1 rounded-lg">ลบ</button>
                    </div>
                  )}
                </div>
              ))}
            </div>
          </div>
        ) : null
      ))}

      {msg && <p className="text-xs text-red-500 text-center">{msg}</p>}

      {showForm && (
        <ServiceForm service={editing} cats={cats} branchId={branchId}
          onClose={() => setShowForm(false)} onSaved={() => { setShowForm(false); load() }} />
      )}
      {showCatForm && (
        <CategoryForm onClose={() => setShowCatForm(false)} onSaved={() => { setShowCatForm(false); load() }} sortOrder={cats.length} />
      )}
    </div>
  )
}

function ServiceForm({ service, cats, branchId, onClose, onSaved }: {
  service: Service | null; cats: Category[]; branchId?: string
  onClose: () => void; onSaved: () => void
}) {
  const [name, setName] = useState(service?.name ?? '')
  const [categoryId, setCategoryId] = useState(service?.category.id ?? cats[0]?.id ?? '')
  const [duration, setDuration] = useState(String(service?.durationMins ?? 60))
  const [price, setPrice] = useState(String(service?.price ?? ''))
  const [buffer, setBuffer] = useState(String(service?.bufferMins ?? 10))
  const [isActive, setIsActive] = useState(service?.isActive ?? true)
  const [loading, setLoading] = useState(false)
  const [err, setErr] = useState('')

  const save = async () => {
    if (!name.trim()) { setErr('กรอกชื่อบริการ'); return }
    if (!categoryId) { setErr('เลือกหมวดหมู่'); return }
    const p = Number(price), d = Number(duration)
    if (!p || p <= 0) { setErr('กรอกราคาที่ถูกต้อง'); return }
    if (!d || d <= 0) { setErr('กรอกระยะเวลาที่ถูกต้อง'); return }
    setLoading(true); setErr('')
    try {
      const body = { categoryId, name: name.trim(), durationMins: d, bufferMins: Number(buffer) || 0, price: p, branchId, isActive }
      if (service) await api.put(`/services/${service.id}`, body)
      else await api.post('/services', body)
      onSaved()
    } catch (e: any) { setErr(e?.response?.data?.message ?? 'บันทึกไม่สำเร็จ') }
    finally { setLoading(false) }
  }

  return (
    <Modal title={service ? 'แก้ไขบริการ' : 'เพิ่มบริการ'} onClose={onClose}>
      <div className="space-y-3">
        <Field label="ชื่อบริการ *" value={name} onChange={setName} placeholder="เช่น นวดไทย 60 นาที" />
        <div>
          <label className="block text-xs text-gray-500 dark:text-gray-400 mb-1">หมวดหมู่ *</label>
          <select value={categoryId} onChange={e => setCategoryId(e.target.value)}
            className="w-full text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 bg-white dark:bg-gray-700 dark:text-white">
            {cats.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <Field label="ระยะเวลา (นาที) *" value={duration} onChange={setDuration} type="number" />
          <Field label="ราคา (บาท) *" value={price} onChange={setPrice} type="number" />
        </div>
        <Field label="เวลาพักห้อง (นาที)" value={buffer} onChange={setBuffer} type="number" />
        <label className="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-300">
          <input type="checkbox" checked={isActive} onChange={e => setIsActive(e.target.checked)} /> เปิดใช้งาน
        </label>
      </div>
      {err && <p className="text-red-500 text-xs mt-3 text-center">{err}</p>}
      <button onClick={save} disabled={loading} className="w-full mt-5 py-2.5 bg-violet-600 hover:bg-violet-700 text-white text-sm font-semibold rounded-lg disabled:opacity-50 transition">
        {loading ? 'กำลังบันทึก...' : 'บันทึก'}
      </button>
    </Modal>
  )
}

function CategoryForm({ onClose, onSaved, sortOrder }: { onClose: () => void; onSaved: () => void; sortOrder: number }) {
  const [name, setName] = useState('')
  const [loading, setLoading] = useState(false)
  const [err, setErr] = useState('')
  const save = async () => {
    if (!name.trim()) { setErr('กรอกชื่อหมวดหมู่'); return }
    setLoading(true); setErr('')
    try { await api.post('/service-categories', { name: name.trim(), sortOrder }); onSaved() }
    catch (e: any) { setErr(e?.response?.data?.message ?? 'บันทึกไม่สำเร็จ') }
    finally { setLoading(false) }
  }
  return (
    <Modal title="เพิ่มหมวดหมู่" onClose={onClose}>
      <Field label="ชื่อหมวดหมู่ *" value={name} onChange={setName} placeholder="เช่น นวดไทย, นวดน้ำมัน" />
      {err && <p className="text-red-500 text-xs mt-3 text-center">{err}</p>}
      <button onClick={save} disabled={loading} className="w-full mt-5 py-2.5 bg-violet-600 hover:bg-violet-700 text-white text-sm font-semibold rounded-lg disabled:opacity-50 transition">
        {loading ? 'กำลังบันทึก...' : 'บันทึก'}
      </button>
    </Modal>
  )
}

function Modal({ title, children, onClose }: { title: string; children: React.ReactNode; onClose: () => void }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-xl w-full max-w-sm p-6 max-h-[90vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between mb-5">
          <h2 className="text-lg font-bold text-gray-800 dark:text-white">{title}</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl">✕</button>
        </div>
        {children}
      </div>
    </div>
  )
}

function Field({ label, value, onChange, placeholder, type = 'text' }: {
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
