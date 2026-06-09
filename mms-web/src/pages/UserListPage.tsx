import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { LinkLineQRModal } from '../components/LinkLineQRModal'

interface User {
  id: string
  displayName: string
  avatarUrl?: string
  isActive: boolean
  lastLoginAt?: string
  roles: string[]
}

export default function UserListPage({
  onSelectUser
}: {
  onSelectUser: (id: string, readOnly: boolean) => void
}) {
  const [users, setUsers] = useState<User[]>([])
  const [callerId, setCallerId] = useState<string>('')
  const [callerPermissions, setCallerPermissions] = useState<string[]>([])
  const [qrUser, setQrUser] = useState<{ id: string; name: string } | null>(null)

  useEffect(() => {
    api.get('/user').then(res => setUsers(res.data))

    const token = localStorage.getItem('accessToken')
    if (token) {
      try {
        const payload = JSON.parse(atob(token.split('.')[1]))
        const id = payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ?? ''
        const perms = Array.isArray(payload.permission) ? payload.permission : []
        setCallerId(id)
        setCallerPermissions(perms)
      } catch {}
    }
  }, [])

  // Owner = มีสิทธิ์ครบทุกอย่าง ใช้ USER_ROLE_ASSIGN เป็นตัวชี้วัดว่าแก้สิทธิ์คนอื่นได้
  const callerCanAssign = callerPermissions.includes('USER_ROLE_ASSIGN')

  return (
    <div className="p-4 space-y-3">
      <h1 className="text-lg font-bold dark:text-white">ผู้ใช้งาน</h1>
      <div className="bg-white dark:bg-gray-800 rounded-xl divide-y dark:divide-gray-700">
        {users.map(u => {
          const isSelf = u.id === callerId

          // แก้ได้: ไม่ใช่ตัวเอง และมีสิทธิ์ USER_ROLE_ASSIGN
          const canManage = !isSelf && callerCanAssign

          return (
            <div key={u.id} className="flex items-center justify-between px-4 py-3">
              <div className="flex items-center gap-3">
                <div className="w-9 h-9 rounded-full bg-emerald-100 flex items-center justify-center text-emerald-700 text-sm font-medium">
                  {u.displayName.charAt(0).toUpperCase()}
                </div>
                <div>
                  <p className="text-sm font-medium dark:text-white">{u.displayName}</p>
                  <p className="text-xs text-gray-400">{u.roles.join(', ') || 'ไม่มีบทบาท'}</p>
                </div>
              </div>
              <div className="flex items-center gap-2">
                {canManage && (
                  <button
                    onClick={() => setQrUser({ id: u.id, name: u.displayName })}
                    className="text-xs border border-[#06C755] text-[#06C755] px-3 py-1.5 rounded-lg hover:bg-green-50 dark:hover:bg-green-900/20 transition"
                  >
                    ผูก LINE
                  </button>
                )}
                <button
                  onClick={() => onSelectUser(u.id, !canManage)}
                  className={`text-xs border px-3 py-1.5 rounded-lg transition ${
                    canManage
                      ? 'text-emerald-600 dark:text-emerald-400 border-emerald-300 dark:border-emerald-700'
                      : 'text-gray-400 dark:text-gray-500 border-gray-200 dark:border-gray-600'
                  }`}
                >
                  {canManage ? 'จัดการสิทธิ์' : 'ดูสิทธิ์'}
                </button>
              </div>
            </div>
          )
        })}
      </div>

      {qrUser && (
        <LinkLineQRModal
          userId={qrUser.id}
          userName={qrUser.name}
          onClose={() => setQrUser(null)}
        />
      )}
    </div>
  )
}