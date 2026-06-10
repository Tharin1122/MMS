import { useState } from 'react'
import { api } from '../api/client'

export interface ManagedUser {
  id: string
  displayName: string
  isActive: boolean
  hasLine: boolean
  hasPassword: boolean
  roles: string[]
}

interface Props {
  user: ManagedUser
  canManage: boolean
  onClose: () => void
  onChanged: () => void          // refetch list
  onManagePerms: () => void
  onLinkLine: () => void
}

export function UserActionsModal({ user, canManage, onClose, onChanged, onManagePerms, onLinkLine }: Props) {
  const [mode, setMode] = useState<'menu' | 'reset' | 'delete' | 'detail'>('menu')
  const [tempPw, setTempPw] = useState('')
  const [loading, setLoading] = useState(false)
  const [detail, setDetail] = useState<any | null>(null)
  const [msg, setMsg] = useState<{ type: 'ok' | 'err'; text: string } | null>(null)

  const openDetail = async () => {
    setMode('detail'); setDetail(null)
    try { setDetail((await api.get(`/user/${user.id}`)).data) }
    catch { setDetail({ error: true }) }
  }

  const run = async (fn: () => Promise<any>, okText: string) => {
    setLoading(true); setMsg(null)
    try { await fn(); setMsg({ type: 'ok', text: okText }); onChanged() }
    catch (err: any) { setMsg({ type: 'err', text: err?.response?.data?.message ?? 'ทำรายการไม่สำเร็จ' }) }
    finally { setLoading(false) }
  }

  const toggleBlock = () => run(
    () => api.put(`/user/${user.id}`, { isActive: !user.isActive }),
    user.isActive ? 'บล็อกผู้ใช้แล้ว' : 'ปลดบล็อกแล้ว'
  )

  const resetPassword = () => run(
    () => api.post(`/user/${user.id}/set-password`, { newPassword: tempPw || null }),
    tempPw ? 'ตั้งรหัสชั่วคราวแล้ว' : 'ล้างรหัสผ่านแล้ว'
  ).then(() => { setTempPw(''); setMode('menu') })

  const remove = () => run(
    () => api.delete(`/user/${user.id}`),
    'ลบพนักงานแล้ว'
  ).then(() => setTimeout(onClose, 800))

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-xl w-full max-w-sm p-6" onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between mb-1">
          <h2 className="text-lg font-bold text-gray-800 dark:text-white">{user.displayName}</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl">✕</button>
        </div>
        <p className="text-xs text-gray-400 mb-4">
          {user.roles.join(', ') || 'ไม่มีบทบาท'} · {user.isActive ? '🟢 ใช้งาน' : '🔴 ถูกบล็อก'} ·{' '}
          {user.hasLine ? 'ผูก LINE' : 'ยังไม่ผูก LINE'}
        </p>

        {mode === 'menu' && (
          <div className="space-y-2">
            <ActionBtn onClick={openDetail} icon="📄">ดูข้อมูลทั้งหมด</ActionBtn>
            <ActionBtn onClick={onManagePerms} icon="🔑">{canManage ? 'จัดการสิทธิ์' : 'ดูสิทธิ์'}</ActionBtn>
            {canManage && <ActionBtn onClick={onLinkLine} icon="🟢">{user.hasLine ? 'เปลี่ยน LINE ที่ผูก' : 'สร้าง QR ผูก LINE'}</ActionBtn>}
            {canManage && <ActionBtn onClick={() => setMode('reset')} icon="🔄">รีเซ็ต/ตั้งรหัสผ่าน</ActionBtn>}
            {canManage && (
              <ActionBtn onClick={toggleBlock} icon={user.isActive ? '🚫' : '✅'} disabled={loading}>
                {user.isActive ? 'บล็อกผู้ใช้' : 'ปลดบล็อก'}
              </ActionBtn>
            )}
            {canManage && <ActionBtn onClick={() => setMode('delete')} icon="🗑️" danger>ลบพนักงาน</ActionBtn>}
          </div>
        )}

        {mode === 'reset' && (
          <div className="space-y-3">
            <p className="text-sm text-gray-600 dark:text-gray-300">ตั้งรหัสผ่านชั่วคราว (บอกพนักงานให้เปลี่ยนเองภายหลัง) หรือเว้นว่างเพื่อล้างรหัส</p>
            <input value={tempPw} onChange={e => setTempPw(e.target.value)} type="text" placeholder="รหัสชั่วคราว (อย่างน้อย 6 ตัว)"
              className="w-full text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-300" />
            <div className="flex gap-2">
              <button onClick={() => setMode('menu')} className="flex-1 py-2 border border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300 text-sm rounded-lg">ยกเลิก</button>
              <button onClick={resetPassword} disabled={loading} className="flex-1 py-2 bg-emerald-600 text-white text-sm rounded-lg disabled:opacity-50">
                {loading ? '...' : (tempPw ? 'ตั้งรหัส' : 'ล้างรหัส')}
              </button>
            </div>
          </div>
        )}

        {mode === 'delete' && (
          <div className="space-y-3">
            <p className="text-sm text-red-600">ยืนยันลบ <b>{user.displayName}</b>? การลบจะปิดการใช้งานบัญชีนี้</p>
            <div className="flex gap-2">
              <button onClick={() => setMode('menu')} className="flex-1 py-2 border border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300 text-sm rounded-lg">ยกเลิก</button>
              <button onClick={remove} disabled={loading} className="flex-1 py-2 bg-red-600 text-white text-sm rounded-lg disabled:opacity-50">
                {loading ? '...' : 'ยืนยันลบ'}
              </button>
            </div>
          </div>
        )}

        {mode === 'detail' && (
          <div>
            {!detail ? (
              <p className="text-sm text-gray-400 text-center py-6">กำลังโหลด...</p>
            ) : detail.error ? (
              <p className="text-sm text-red-500 text-center py-6">โหลดข้อมูลไม่สำเร็จ</p>
            ) : (
              <div className="space-y-2 text-sm">
                <Row label="ชื่อที่แสดง" value={detail.displayName} />
                <Row label="ชื่อผู้ใช้ (login)" value={detail.username ?? '— ยังไม่ตั้ง —'} />
                <Row label="เบอร์โทร" value={detail.phone ?? '—'} />
                <Row label="อีเมล" value={detail.email ?? '—'} />
                <Row label="บทบาท" value={(detail.roles ?? []).join(', ') || 'ไม่มี'} />
                <Row label="จำนวนสิทธิ์" value={`${detail.permissionCount} สิทธิ์`} />
                <Row label="สาขา" value={detail.branch ?? '—'} />
                <Row label="ผูก LINE" value={detail.hasLine ? '🟢 ผูกแล้ว' : '⚪ ยังไม่ผูก'} />
                <Row label="ตั้งรหัสผ่าน" value={detail.hasPassword ? '✅ ตั้งแล้ว' : '❌ ยังไม่ตั้ง'} />
                <Row label="สถานะ" value={detail.isActive ? '🟢 ใช้งาน' : '🔴 ถูกบล็อก'} />
                <Row label="เข้าระบบล่าสุด" value={detail.lastLoginAt ? new Date(detail.lastLoginAt).toLocaleString('th-TH') : 'ยังไม่เคย'} />
                <Row label="สร้างเมื่อ" value={detail.createdAt ? new Date(detail.createdAt).toLocaleDateString('th-TH') : '—'} />
              </div>
            )}
            <button onClick={() => setMode('menu')} className="w-full mt-4 py-2 border border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300 text-sm rounded-lg">← กลับ</button>
          </div>
        )}

        {msg && <p className={`text-xs mt-3 text-center ${msg.type === 'ok' ? 'text-emerald-600' : 'text-red-500'}`}>{msg.text}</p>}
      </div>
    </div>
  )
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between gap-3 py-1.5 border-b border-gray-50 dark:border-gray-700">
      <span className="text-gray-400 flex-shrink-0">{label}</span>
      <span className="text-gray-800 dark:text-gray-100 text-right">{value}</span>
    </div>
  )
}

function ActionBtn({ children, onClick, icon, danger, disabled }: { children: React.ReactNode; onClick: () => void; icon: string; danger?: boolean; disabled?: boolean }) {
  return (
    <button onClick={onClick} disabled={disabled}
      className={`w-full flex items-center gap-3 px-4 py-2.5 rounded-lg text-sm text-left transition disabled:opacity-50
        ${danger ? 'text-red-600 hover:bg-red-50 dark:hover:bg-red-900/20' : 'text-gray-700 dark:text-gray-200 hover:bg-gray-50 dark:hover:bg-gray-700'}`}>
      <span>{icon}</span>{children}
    </button>
  )
}
