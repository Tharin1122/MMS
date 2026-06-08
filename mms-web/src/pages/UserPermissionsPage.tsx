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

export default function UserPermissionsPage({ userId }: { userId: string }) {
  const [groups, setGroups] = useState<PermissionGroup[]>([])
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    api.get(`/user/${userId}/permissions`).then(res => {
      setGroups(res.data)
      // เอา permission ที่ user มีอยู่แล้วใส่ selected
      const existing = new Set<string>()
      res.data.forEach((g: PermissionGroup) =>
        g.permissions.forEach(p => { if (p.userHas) existing.add(p.code) })
      )
      setSelected(existing)
    })
  }, [userId])

  const toggle = (code: string, callerHas: boolean) => {
    if (!callerHas) return // ถ้าตัวเองไม่มีสิทธิ์ ทำอะไรไม่ได้
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
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="p-4 space-y-6">
      <div className="flex justify-between items-center">
        <h1 className="text-lg font-bold dark:text-white">จัดการสิทธิ์</h1>
        <button
          onClick={save}
          disabled={saving}
          className="bg-emerald-500 text-white px-4 py-2 rounded-lg text-sm"
        >
          {saving ? 'กำลังบันทึก...' : 'บันทึก'}
        </button>
      </div>

      {groups.map(group => (
        <div key={group.group}>
          <h2 className="text-sm font-semibold text-gray-500 dark:text-gray-400 mb-2">
            {group.group}
          </h2>
          <div className="bg-white dark:bg-gray-800 rounded-xl divide-y dark:divide-gray-700">
            {group.permissions.map(p => (
              <div
                key={p.code}
                className="flex items-center justify-between px-4 py-3"
              >
                <div>
                  <p className={`text-sm font-medium ${
                    p.callerHas
                      ? 'text-gray-800 dark:text-white'
                      : 'text-gray-400 dark:text-gray-600' // dim ถ้า caller ไม่มีสิทธิ์
                  }`}>
                    {p.description}
                  </p>
                  <p className="text-xs text-gray-400">{p.code}</p>
                </div>

                {/* Toggle button */}
                <button
                  onClick={() => toggle(p.code, p.callerHas)}
                  disabled={!p.callerHas}
                  className={`relative w-12 h-6 rounded-full transition-colors ${
                    selected.has(p.code)
                      ? p.callerHas ? 'bg-emerald-500' : 'bg-emerald-300'
                      : 'bg-gray-300 dark:bg-gray-600'
                  } ${!p.callerHas ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}`}
                >
                  <span className={`absolute top-1 w-4 h-4 bg-white rounded-full shadow transition-transform ${
                    selected.has(p.code) ? 'translate-x-7' : 'translate-x-1'
                  }`} />
                </button>
              </div>
            ))}
          </div>
        </div>
      ))}
    </div>
  )
}