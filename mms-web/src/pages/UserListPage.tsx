import { useEffect, useState } from 'react'
import { api } from '../api/client'
import { LinkLineQRModal } from '../components/LinkLineQRModal'
import { CreateUserModal } from '../components/CreateUserModal'
import { UserActionsModal, type ManagedUser } from '../components/UserActionsModal'

interface User extends ManagedUser {
  avatarUrl?: string
  lastLoginAt?: string
  phone?: string
  username?: string
}

const ROLE_LABEL: Record<string, string> = {
  Owner: 'เจ้าของร้าน', Manager: 'ผู้จัดการ', Reception: 'พนักงานต้อนรับ',
  Cashier: 'แคชเชียร์', Therapist: 'หมอนวด',
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
  const [actionUser, setActionUser] = useState<User | null>(null)
  const [showCreate, setShowCreate] = useState(false)

  const load = () => { api.get('/user').then(res => setUsers(res.data)) }

  useEffect(() => {
    load()
    const token = localStorage.getItem('accessToken')
    if (token) {
      try {
        const payload = JSON.parse(atob(token.split('.')[1]))
        const id = payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ?? ''
        const perms = Array.isArray(payload.permission) ? payload.permission : []
        setCallerId(id); setCallerPermissions(perms)
      } catch {}
    }
  }, [])

  const callerCanAssign = callerPermissions.includes('USER_ROLE_ASSIGN')
  const callerCanCreate = callerPermissions.includes('USER_CREATE')

  return (
    <div className="p-4 space-y-3">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-bold dark:text-white">ผู้ใช้งาน <span className="text-sm font-normal text-gray-400">({users.length})</span></h1>
        {callerCanCreate && (
          <button onClick={() => setShowCreate(true)}
            className="px-3 py-1.5 bg-violet-600 hover:bg-violet-700 text-white text-sm rounded-lg transition">
            + เพิ่มพนักงาน
          </button>
        )}
      </div>

      <div className="bg-white dark:bg-gray-800 rounded-xl divide-y dark:divide-gray-700">
        {users.map(u => {
          const isSelf = u.id === callerId
          const canManage = !isSelf && callerCanAssign
          return (
            <div key={u.id} className="flex items-center justify-between px-4 py-3">
              <div className="flex items-center gap-3 min-w-0">
                <div className="w-9 h-9 rounded-full bg-emerald-100 flex items-center justify-center text-emerald-700 text-sm font-medium overflow-hidden flex-shrink-0">
                  {u.avatarUrl ? <img src={u.avatarUrl} alt="" className="w-full h-full object-cover" /> : u.displayName.charAt(0).toUpperCase()}
                </div>
                <div className="min-w-0">
                  <p className="text-sm font-medium dark:text-white truncate flex items-center gap-1.5">
                    {u.displayName}
                    {!u.isActive && <span className="text-xs bg-red-100 text-red-600 px-1.5 rounded">บล็อก</span>}
                  </p>
                  <p className="text-xs text-gray-400 truncate">
                    {u.roles.map(r => ROLE_LABEL[r] ?? r).join(', ') || 'ไม่มีบทบาท'}
                    {u.hasLine && ' · 🟢 LINE'}
                  </p>
                </div>
              </div>
              <div className="flex items-center gap-2 flex-shrink-0">
                {isSelf
                  ? <span className="text-xs text-gray-400">ฉัน</span>
                  : (
                    <button onClick={() => setActionUser(u)}
                      className="text-xs border border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300 px-3 py-1.5 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition">
                      จัดการ
                    </button>
                  )}
              </div>
            </div>
          )
        })}
      </div>

      {showCreate && (
        <CreateUserModal
          onClose={() => setShowCreate(false)}
          onCreated={(id, name) => { setShowCreate(false); load(); setQrUser({ id, name }) }}
        />
      )}

      {actionUser && (
        <UserActionsModal
          user={actionUser}
          canManage={!(actionUser.id === callerId) && callerCanAssign}
          onClose={() => setActionUser(null)}
          onChanged={load}
          onManagePerms={() => { const a = actionUser; setActionUser(null); onSelectUser(a.id, !(callerCanAssign && a.id !== callerId)) }}
          onLinkLine={() => { const a = actionUser; setActionUser(null); setQrUser({ id: a.id, name: a.displayName }) }}
        />
      )}

      {qrUser && (
        <LinkLineQRModal
          userId={qrUser.id}
          userName={qrUser.name}
          onClose={() => setQrUser(null)}
          onLinked={load}
        />
      )}
    </div>
  )
}
