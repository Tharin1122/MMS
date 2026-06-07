import { useState, useEffect } from 'react'
import { api } from '../api/client'
import { t } from '../i18n/th'

// ── Types ──────────────────────────────────────────
interface Customer {
  id: string
  displayName: string
  phone: string | null
}

interface Service {
  id: string
  name: string
  durationMins: number
  price: number
  category: { name: string }
}

interface Therapist {
  id: string
  displayName: string
  code: string | null
  avatarUrl: string | null
}

interface SelectedItem {
  service: Service
  therapistId: string | null // null = Auto
  sortOrder: number
}

type Step = 'customer' | 'services' | 'therapist' | 'confirm' | 'done'

// ── Main Component ─────────────────────────────────
export default function WalkInPage() {
  const [step, setStep] = useState<Step>('customer')

  // Customer
  const [searchPhone, setSearchPhone] = useState('')
  const [searchResult, setSearchResult] = useState<Customer | null>(null)
  const [searching, setSearching] = useState(false)
  const [newCustomerName, setNewCustomerName] = useState('')
  const [selectedCustomer, setSelectedCustomer] = useState<Customer | null>(null)

  // Services
  const [services, setServices] = useState<Service[]>([])
  const [selectedItems, setSelectedItems] = useState<SelectedItem[]>([])

  // Therapists
  const [availableMap, setAvailableMap] = useState<Record<string, Therapist[]>>({})

  // Result
  const [result, setResult] = useState<{ queueNo: string; estimatedWaitMins: number } | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  // โหลด services
  useEffect(() => {
    api.get('/services?activeOnly=true').then(res => setServices(res.data))
  }, [])

  // ── Step 1: ค้นหา Customer ──────────────────────
  const searchCustomer = async () => {
    if (!searchPhone.trim()) return
    setSearching(true)
    setSearchResult(null)
    try {
      const res = await api.get(`/customer?search=${searchPhone}&pageSize=1`)
      const items = res.data.items ?? res.data
      setSearchResult(items.length > 0 ? items[0] : null)
      if (items.length === 0) setNewCustomerName('')
    } catch {
      setSearchResult(null)
    } finally {
      setSearching(false)
    }
  }

  const createAndSelectCustomer = async () => {
    if (!newCustomerName.trim()) return
    setLoading(true)
    try {
      const res = await api.post('/customer', {
        displayName: newCustomerName,
        phone: searchPhone || null
      })
      setSelectedCustomer({ id: res.data.id, displayName: newCustomerName, phone: searchPhone || null })
      setStep('services')
    } catch {
      setError('สร้างลูกค้าไม่สำเร็จ')
    } finally {
      setLoading(false)
    }
  }

  // ── Step 2: เลือกบริการ ─────────────────────────
  const toggleService = (service: Service) => {
    setSelectedItems(prev => {
      const exists = prev.find(i => i.service.id === service.id)
      if (exists) return prev.filter(i => i.service.id !== service.id)
      return [...prev, { service, therapistId: null, sortOrder: prev.length }]
    })
  }

  const goToTherapistStep = async () => {
    if (selectedItems.length === 0) return
    setLoading(true)
    try {
      const serviceIds = selectedItems.map(i => i.service.id)
      const params = serviceIds.map(id => `serviceIds=${id}`).join('&')
      const res = await api.get(`/walk-in/available-therapists?${params}`)
      setAvailableMap(res.data)
      setStep('therapist')
    } catch {
      setError('โหลดหมอนวดไม่สำเร็จ')
    } finally {
      setLoading(false)
    }
  }

  // ── Step 3: เลือกหมอนวด ────────────────────────
  const setTherapist = (serviceId: string, therapistId: string | null) => {
    setSelectedItems(prev =>
      prev.map(i => i.service.id === serviceId ? { ...i, therapistId } : i)
    )
  }

  // ── Step 4: ยืนยัน ──────────────────────────────
  const createWalkIn = async () => {
    if (!selectedCustomer) return
    setLoading(true)
    setError('')
    try {
      const res = await api.post('/walk-in', {
        customerId: selectedCustomer.id,
        items: selectedItems.map((item, idx) => ({
          serviceId: item.service.id,
          therapistId: item.therapistId ?? null,
          roomId: null,
          sortOrder: idx
        }))
      })
      setResult({
        queueNo: res.data.queueNo,
        estimatedWaitMins: res.data.estimatedWaitMins ?? 0
      })
      setStep('done')
    } catch (err: any) {
      setError(err.response?.data?.message ?? 'เกิดข้อผิดพลาด')
    } finally {
      setLoading(false)
    }
  }

  const reset = () => {
    setStep('customer')
    setSearchPhone('')
    setSearchResult(null)
    setNewCustomerName('')
    setSelectedCustomer(null)
    setSelectedItems([])
    setAvailableMap({})
    setResult(null)
    setError('')
  }

  const totalPrice = selectedItems.reduce((sum, i) => sum + i.service.price, 0)
  const totalMins = selectedItems.reduce((sum, i) => sum + i.service.durationMins, 0)

  // ── Render ──────────────────────────────────────
  return (
    <div className="max-w-lg mx-auto px-4 py-6">
      {/* Progress */}
      <div className="flex items-center gap-2 mb-6">
        {(['customer', 'services', 'therapist', 'confirm'] as Step[]).map((s, idx) => (
          <div key={s} className="flex items-center gap-2 flex-1">
            <div className={`w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold
              ${step === s ? 'bg-emerald-500 text-white' :
                ['customer', 'services', 'therapist', 'confirm', 'done'].indexOf(step) > idx
                  ? 'bg-emerald-200 text-emerald-700' : 'bg-gray-200 text-gray-500'}`}>
              {idx + 1}
            </div>
            {idx < 3 && <div className="flex-1 h-0.5 bg-gray-200" />}
          </div>
        ))}
      </div>

      {error && (
        <div className="mb-4 p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-xl text-red-600 dark:text-red-400 text-sm">
          {error}
        </div>
      )}

      {/* ── Step 1: Customer ── */}
      {step === 'customer' && (
        <Card title="👤 ลูกค้า">
          <div className="flex gap-2 mb-4">
            <input
              type="tel"
              placeholder="เบอร์โทรลูกค้า"
              value={searchPhone}
              onChange={e => setSearchPhone(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && searchCustomer()}
              className={inputClass}
            />
            <button onClick={searchCustomer} disabled={searching} className={btnPrimary}>
              {searching ? '...' : 'ค้นหา'}
            </button>
          </div>

          {searchResult && (
            <div
              className="p-3 bg-emerald-50 dark:bg-emerald-900/20 border border-emerald-200 dark:border-emerald-800 rounded-xl cursor-pointer mb-3"
              onClick={() => { setSelectedCustomer(searchResult); setStep('services') }}
            >
              <p className="font-medium text-gray-800 dark:text-white">{searchResult.displayName}</p>
              <p className="text-xs text-gray-500">{searchResult.phone}</p>
              <p className="text-xs text-emerald-600 mt-1">แตะเพื่อเลือก</p>
            </div>
          )}

          {searchResult === null && searchPhone && !searching && (
            <div className="space-y-2">
              <p className="text-sm text-gray-500">ไม่พบลูกค้า — สร้างใหม่</p>
              <input
                type="text"
                placeholder="ชื่อลูกค้า"
                value={newCustomerName}
                onChange={e => setNewCustomerName(e.target.value)}
                className={inputClass}
              />
              <button
                onClick={createAndSelectCustomer}
                disabled={!newCustomerName.trim() || loading}
                className={`w-full ${btnPrimary}`}
              >
                {loading ? 'กำลังสร้าง...' : 'สร้างและเลือก'}
              </button>
            </div>
          )}

          <button
            onClick={() => { setSelectedCustomer({ id: '', displayName: 'ลูกค้าทั่วไป', phone: null }); setStep('services') }}
            className="w-full mt-3 py-2 text-sm text-gray-500 hover:text-gray-700 dark:hover:text-gray-300 transition"
          >
            ข้ามไป (ลูกค้าทั่วไป)
          </button>
        </Card>
      )}

      {/* ── Step 2: Services ── */}
      {step === 'services' && (
        <Card title="💆 เลือกบริการ">
          <p className="text-xs text-gray-500 mb-3">ลูกค้า: <span className="font-medium text-gray-700 dark:text-gray-300">{selectedCustomer?.displayName}</span></p>

          <div className="space-y-2 mb-4">
            {services.map(svc => {
              const selected = selectedItems.some(i => i.service.id === svc.id)
              return (
                <div
                  key={svc.id}
                  onClick={() => toggleService(svc)}
                  className={`p-3 rounded-xl border cursor-pointer transition
                    ${selected
                      ? 'border-emerald-500 bg-emerald-50 dark:bg-emerald-900/20'
                      : 'border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 hover:border-emerald-300'}`}
                >
                  <div className="flex justify-between items-center">
                    <div>
                      <p className="text-sm font-medium text-gray-800 dark:text-white">{svc.name}</p>
                      <p className="text-xs text-gray-400">{svc.durationMins} นาที · {svc.category.name}</p>
                    </div>
                    <div className="text-right">
                      <p className="text-sm font-bold text-emerald-600">฿{svc.price}</p>
                      {selected && <span className="text-xs text-emerald-500">✓ เลือกแล้ว</span>}
                    </div>
                  </div>
                </div>
              )
            })}
          </div>

          {selectedItems.length > 0 && (
            <div className="text-xs text-gray-500 mb-3">
              เลือก {selectedItems.length} บริการ · รวม {totalMins} นาที · ฿{totalPrice}
            </div>
          )}

          <div className="flex gap-2">
            <button onClick={() => setStep('customer')} className={btnSecondary}>← ย้อนกลับ</button>
            <button
              onClick={goToTherapistStep}
              disabled={selectedItems.length === 0 || loading}
              className={`flex-1 ${btnPrimary}`}
            >
              {loading ? '...' : 'ถัดไป →'}
            </button>
          </div>
        </Card>
      )}

      {/* ── Step 3: Therapist ── */}
      {step === 'therapist' && (
        <Card title="🧑‍⚕️ เลือกหมอนวด">
          <div className="space-y-4 mb-4">
            {selectedItems.map(item => (
              <div key={item.service.id}>
                <p className="text-xs font-semibold text-gray-600 dark:text-gray-400 mb-2">
                  {item.service.name}
                </p>
                <div className="grid grid-cols-2 gap-2">
                  {/* Auto option */}
                  <div
                    onClick={() => setTherapist(item.service.id, null)}
                    className={`p-2 rounded-xl border cursor-pointer text-center transition
                      ${item.therapistId === null
                        ? 'border-emerald-500 bg-emerald-50 dark:bg-emerald-900/20'
                        : 'border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800'}`}
                  >
                    <div className="text-xl mb-1">🎲</div>
                    <p className="text-xs font-medium text-gray-700 dark:text-gray-300">Auto</p>
                    <p className="text-xs text-gray-400">ระบบเลือกให้</p>
                  </div>

                  {/* Manual options */}
                  {(availableMap[item.service.id] ?? []).map(th => (
                    <div
                      key={th.id}
                      onClick={() => setTherapist(item.service.id, th.id)}
                      className={`p-2 rounded-xl border cursor-pointer text-center transition
                        ${item.therapistId === th.id
                          ? 'border-emerald-500 bg-emerald-50 dark:bg-emerald-900/20'
                          : 'border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800'}`}
                    >
                      <div className="w-8 h-8 rounded-full bg-gray-200 dark:bg-gray-600 mx-auto mb-1 flex items-center justify-center text-sm">
                        {th.avatarUrl ? <img src={th.avatarUrl} className="w-full h-full rounded-full object-cover" /> : '👤'}
                      </div>
                      <p className="text-xs font-medium text-gray-700 dark:text-gray-300 truncate">{th.displayName}</p>
                    </div>
                  ))}

                  {(availableMap[item.service.id] ?? []).length === 0 && (
                    <div className="col-span-1 p-2 rounded-xl border border-gray-200 dark:border-gray-700 text-center opacity-50">
                      <p className="text-xs text-gray-400">ไม่มีหมอว่าง</p>
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>

          <div className="flex gap-2">
            <button onClick={() => setStep('services')} className={btnSecondary}>← ย้อนกลับ</button>
            <button onClick={() => setStep('confirm')} className={`flex-1 ${btnPrimary}`}>
              ถัดไป →
            </button>
          </div>
        </Card>
      )}

      {/* ── Step 4: Confirm ── */}
      {step === 'confirm' && (
        <Card title="✅ ยืนยันการรับบริการ">
          <div className="space-y-3 mb-4">
            <Row label="ลูกค้า" value={selectedCustomer?.displayName ?? '-'} />
            {selectedItems.map(item => (
              <div key={item.service.id} className="bg-gray-50 dark:bg-gray-800 rounded-xl p-3">
                <div className="flex justify-between">
                  <p className="text-sm font-medium text-gray-800 dark:text-white">{item.service.name}</p>
                  <p className="text-sm font-bold text-emerald-600">฿{item.service.price}</p>
                </div>
                <p className="text-xs text-gray-400 mt-0.5">
                  {item.service.durationMins} นาที ·
                  หมอนวด: {item.therapistId
                    ? availableMap[item.service.id]?.find(t => t.id === item.therapistId)?.displayName
                    : 'Auto'}
                </p>
              </div>
            ))}
            <div className="border-t border-gray-200 dark:border-gray-700 pt-2 flex justify-between">
              <span className="text-sm font-semibold text-gray-700 dark:text-gray-300">รวม</span>
              <span className="text-sm font-bold text-emerald-600">฿{totalPrice}</span>
            </div>
          </div>

          {error && <p className="text-red-500 text-sm mb-3">{error}</p>}

          <div className="flex gap-2">
            <button onClick={() => setStep('therapist')} className={btnSecondary}>← ย้อนกลับ</button>
            <button onClick={createWalkIn} disabled={loading} className={`flex-1 ${btnPrimary}`}>
              {loading ? 'กำลังสร้างคิว...' : '🎫 รับคิว'}
            </button>
          </div>
        </Card>
      )}

      {/* ── Done ── */}
      {step === 'done' && result && (
        <Card title="">
          <div className="text-center py-4">
            <div className="text-6xl mb-4">🎉</div>
            <p className="text-gray-500 dark:text-gray-400 text-sm mb-2">เลขคิว</p>
            <p className="text-7xl font-black text-emerald-500 mb-4">{result.queueNo}</p>
            {result.estimatedWaitMins > 0 && (
              <p className="text-gray-500 dark:text-gray-400 text-sm">
                รอประมาณ <span className="font-bold text-orange-500">{result.estimatedWaitMins} นาที</span>
              </p>
            )}
            <button onClick={reset} className={`mt-6 w-full ${btnPrimary}`}>
              รับลูกค้าคนต่อไป
            </button>
          </div>
        </Card>
      )}
    </div>
  )
}

// ── UI Helpers ──────────────────────────────────────
function Card({ title, children }: { title: string, children: React.ReactNode }) {
  return (
    <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-sm border border-gray-100 dark:border-gray-700 p-5">
      {title && <h2 className="text-lg font-bold text-gray-800 dark:text-white mb-4">{title}</h2>}
      {children}
    </div>
  )
}

function Row({ label, value }: { label: string, value: string }) {
  return (
    <div className="flex justify-between text-sm">
      <span className="text-gray-500 dark:text-gray-400">{label}</span>
      <span className="font-medium text-gray-800 dark:text-white">{value}</span>
    </div>
  )
}

const inputClass = "flex-1 border border-gray-300 dark:border-gray-600 rounded-xl px-3 py-2 text-sm bg-white dark:bg-gray-700 text-gray-800 dark:text-white focus:outline-none focus:border-emerald-400"
const btnPrimary = "py-2 px-4 bg-emerald-500 hover:bg-emerald-600 text-white text-sm font-semibold rounded-xl transition disabled:opacity-50"
const btnSecondary = "py-2 px-4 bg-gray-100 dark:bg-gray-700 hover:bg-gray-200 dark:hover:bg-gray-600 text-gray-700 dark:text-gray-300 text-sm font-semibold rounded-xl transition"