import { useEffect, useState } from 'react'
import { api } from '../api/client'

interface PermissionItem {
  id: string
  code: string
  description: string
  userHas: boolean
  callerHas: boolean
}

interface PermissionGroup {
  group: string
  permissions: PermissionItem[]
}

export default function UserPermissionsPage({
  userId,
  onBack,
  readOnly = false,
}: {
  userId: string
  onBack?: () => void
  readOnly?: boolean
}) {
  const [groups, setGroups] = useState<PermissionGroup[]>([])
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [saving, setSaving] = useState(false)
  const [toast, setToast] = useState<'success' | 'error' | null>(null)
  const [showConfirm, setShowConfirm] = useState(false)

  useEffect(() => {
    api.get(`/user/${userId}/permissions`).then(res => {
      setGroups(res.data)
      const existing = new Set<string>()
      res.data.forEach((g: PermissionGroup) =>
        g.permissions.forEach(p => { if (p.userHas) existing.add(p.code) })
      )
      setSelected(existing)
    })
  }, [userId])

  const toggle = (code: string, callerHas: boolean) => {
    if (readOnly || !callerHas) return
    setSelected(prev => {
      const next = new Set(prev)
      next.has(code) ? next.delete(code) : next.add(code)
      return next
    })
  }

  const save = async () => {
    setSaving(true)
    try {
      await api.put(`/user/${userId}/permissions`, {
        permissionCodes: Array.from(selected)
      })
      const res = await api.get(`/user/${userId}/permissions`)
      setGroups(res.data)
      const updated = new Set<string>()
      res.data.forEach((g: PermissionGroup) =>
        g.permissions.forEach(p => { if (p.userHas) updated.add(p.code) })
      )
      setSelected(updated)
      setToast('success')
    } catch {
      setToast('error')
    } finally {
      setSaving(false)
      setTimeout(() => setToast(null), 3000)
    }
  }

  return (
    <div className="p-4 space-y-6">
      <div className="flex justify-between items-center">
        <div className="flex items-center gap-2">
          {onBack && (
            <button
              onClick={onBack}
              className="text-gray-400 dark:text-gray-500 text-2xl leading-none px-1"
            >
              ‹
            </button>
          )}
          <div>
            <h1 className="text-lg font-bold dark:text-white">
              {readOnly ? 'ดูสิทธิ์' : 'จัดการสิทธิ์'}
            </h1>
            {readOnly && (
              <p className="text-xs text-gray-400">ดูได้อย่างเดียว</p>
            )}
          </div>
        </div>
        {!readOnly && (
          <button
            onClick={() => setShowConfirm(true)}
            disabled={saving}
            className="bg-emerald-500 text-white px-4 py-2 rounded-lg text-sm"
          >
            {saving ? 'กำลังบันทึก...' : 'บันทึก'}
          </button>
        )}
      </div>

      {groups.map(group => (
        <div key={group.group}>
          <h2 className="text-sm font-semibold text-gray-500 dark:text-gray-400 mb-2">
            {group.group}
          </h2>
          <div className="bg-white dark:bg-gray-800 rounded-xl divide-y dark:divide-gray-700">
            {group.permissions.map(p => {
              const isToggleable = !readOnly && p.callerHas
              return (
                <div key={p.code} className="flex items-center justify-between px-4 py-3">
                  <div>
                    <p className={`text-sm font-medium ${
                      p.callerHas
                        ? 'text-gray-800 dark:text-white'
                        : 'text-gray-400 dark:text-gray-600'
                    }`}>
                      {p.description}
                    </p>
                    <p className="text-xs text-gray-400">{p.code}</p>
                  </div>
                  <button
                    onClick={() => toggle(p.code, p.callerHas)}
                    disabled={!isToggleable}
                    className={`relative w-12 h-6 rounded-full transition-colors ${
                      selected.has(p.code)
                        ? isToggleable ? 'bg-emerald-500' : 'bg-emerald-300'
                        : 'bg-gray-300 dark:bg-gray-600'
                    } ${!isToggleable ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}`}
                  >
                    <span className={`absolute top-1 w-4 h-4 bg-white rounded-full shadow transition-transform ${
                      selected.has(p.code) ? 'translate-x-7' : 'translate-x-1'
                    }`} />
                  </button>
                </div>
              )
            })}
          </div>
        </div>
      ))}

      {showConfirm && (
        <div className="fixed inset-0 bg-black/40 flex items-end justify-center z-50 pb-24">
          <div className="bg-white dark:bg-gray-800 rounded-2xl w-full max-w-sm mx-4 p-5 space-y-4">
            <h3 className="text-base font-semibold dark:text-white">ยืนยันการบันทึก</h3>
            <p className="text-sm text-gray-500 dark:text-gray-400">
              ต้องการบันทึกการเปลี่ยนแปลงสิทธิ์ใช่หรือไม่?
            </p>
            <div className="flex gap-3">
              <button
                onClick={() => setShowConfirm(false)}
                className="flex-1 py-2.5 rounded-xl border border-gray-200 dark:border-gray-600 text-sm text-gray-600 dark:text-gray-300"
              >
                ยกเลิก
              </button>
              <button
                onClick={() => { setShowConfirm(false); save() }}
                className="flex-1 py-2.5 rounded-xl bg-emerald-500 text-white text-sm font-medium"
              >
                ยืนยัน
              </button>
            </div>
          </div>
        </div>
      )}

      {toast && (
        <div className={`fixed bottom-24 left-4 right-4 p-3 rounded-xl text-sm text-white text-center ${
          toast === 'success' ? 'bg-emerald-500' : 'bg-red-500'
        }`}>
          {toast === 'success' ? 'บันทึกสำเร็จ ✓' : 'เกิดข้อผิดพลาด กรุณาลองใหม่'}
        </div>
      )}
    </div>
  )
}