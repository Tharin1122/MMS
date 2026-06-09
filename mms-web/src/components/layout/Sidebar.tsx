import { t } from '../../i18n/th'
import { useAuthStore } from '../../store/authStore'

export type Page =
  | 'dashboard' | 'booking' | 'schedule' | 'customer'
  | 'service' | 'therapist' | 'revenue' | 'promotion'
  | 'stock' | 'roles' | 'settings' | 'report' | 'logs'

interface NavItem {
  key: Page
  icon: string
  label: string
  planRequired?: 'basic' | 'shop' | 'business'
  children?: { key: Page; label: string }[]
}

const navItems: NavItem[] = [
  { key: 'dashboard',  icon: '⊞',  label: t('nav.dashboard') },
  { key: 'booking',    icon: '📋', label: t('nav.booking') },
  { key: 'schedule',   icon: '🗓', label: t('nav.schedule') },
  { key: 'customer',   icon: '👤', label: t('nav.customer') },
  { key: 'service',    icon: '💆', label: t('nav.service') },
  { key: 'therapist',  icon: '🧑‍⚕️', label: t('nav.therapist') },
  {
    key: 'revenue', icon: '💰', label: t('nav.finance'),
    children: [
      { key: 'revenue', label: t('nav.revenue') },
    ]
  },
  { key: 'promotion',  icon: '🎁', label: t('nav.promotion'), planRequired: 'shop' },
  { key: 'stock',      icon: '📦', label: t('nav.stock'),      planRequired: 'shop' },
  { key: 'roles',      icon: '🔑', label: t('nav.roles') },
  { key: 'settings',   icon: '⚙️', label: t('nav.settings') },
  { key: 'report',     icon: '📊', label: t('nav.report') },
  { key: 'logs',       icon: '🗂', label: t('nav.logs') },
]

const planLabel: Record<string, string> = {
  Free: t('plan.free'),
  Basic: t('plan.basic'),
  Shop: t('plan.shop'),
  Business: t('plan.business'),
}

const planColor: Record<string, string> = {
  Free: 'bg-gray-500',
  Basic: 'bg-blue-500',
  Shop: 'bg-violet-500',
  Business: 'bg-amber-500',
}

interface SidebarProps {
  currentPage: Page
  onNavigate: (page: Page) => void
  planType?: string
}

export function Sidebar({ currentPage, onNavigate, planType = 'Free' }: SidebarProps) {
  const { user, logout } = useAuthStore()

  return (
    <aside className="w-56 flex-shrink-0 bg-violet-900 text-white flex flex-col h-screen sticky top-0">
      {/* Logo */}
      <div className="px-5 py-5 border-b border-violet-700">
        <div className="flex items-center gap-2 mb-0.5">
          <span className="text-2xl">🌿</span>
          <div>
            <p className="font-bold text-sm leading-tight">BaanSuay</p>
            <p className="text-violet-300 text-xs">Massage & Spa</p>
          </div>
        </div>
      </div>

      {/* Nav */}
      <nav className="flex-1 overflow-y-auto py-3 px-2">
        {navItems.map(item => {
          const isActive = currentPage === item.key ||
            item.children?.some(c => c.key === currentPage)
          const locked = item.planRequired &&
            planType === 'Free' && item.planRequired !== 'free'

          return (
            <button
              key={item.key}
              onClick={() => !locked && onNavigate(item.key)}
              className={`w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm mb-0.5 text-left transition
                ${isActive
                  ? 'bg-violet-600 text-white font-medium'
                  : locked
                    ? 'text-violet-500 cursor-not-allowed'
                    : 'text-violet-200 hover:bg-violet-800 hover:text-white'
                }`}
              title={locked ? t('plan.locked') + ` (${item.planRequired})` : undefined}
            >
              <span className="text-base w-5 text-center">{item.icon}</span>
              <span className="flex-1 truncate">{item.label}</span>
              {locked && <span className="text-xs opacity-60">🔒</span>}
            </button>
          )
        })}
      </nav>

      {/* Plan badge */}
      <div className="px-4 py-3 border-t border-violet-700">
        <div className="bg-violet-800 rounded-xl p-3 mb-3">
          <div className="flex items-center justify-between mb-1">
            <span className="text-xs text-violet-300">ไลเซนส์ Line OA</span>
            <span className={`text-xs px-1.5 py-0.5 rounded-full text-white ${planColor[planType] ?? 'bg-gray-500'}`}>
              {planLabel[planType] ?? planType}
            </span>
          </div>
          <p className="text-xs text-violet-200 leading-tight">
            เชื่อมต่อ Line Official Account<br />สำหรับแจ้งเตือน & จองคิว
          </p>
          <button className="mt-2 w-full bg-green-500 hover:bg-green-400 text-white text-xs py-1.5 rounded-lg transition">
            เชื่อมต่อแล้ว
          </button>
        </div>

        {/* User info */}
        <div className="flex items-center gap-2">
          <div className="w-8 h-8 rounded-full bg-violet-600 flex items-center justify-center text-sm overflow-hidden">
            {user?.avatarUrl
              ? <img src={user.avatarUrl} className="w-full h-full object-cover" alt="" />
              : '👤'}
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-xs font-medium truncate">{user?.displayName}</p>
            <p className="text-xs text-violet-400">เจ้าของร้าน</p>
          </div>
          <button
            onClick={logout}
            title="ออกจากระบบ"
            className="text-violet-400 hover:text-red-300 text-xs transition"
          >
            ↩
          </button>
        </div>
      </div>
    </aside>
  )
}
