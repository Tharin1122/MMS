import { useState, type ReactNode } from 'react'
import { useAuthStore } from '../store/authStore'

export interface NavItem { k: string; ic: string; t: string }

// nav ตาม Massage-Spa template
export const NAV: NavItem[] = [
  { k: 'dashboard', ic: '🏠', t: 'แดชบอร์ด' },
  { k: 'booking', ic: '📅', t: 'การจอง & คิวงาน' },
  { k: 'pos', ic: '🧾', t: 'ชำระเงิน / ออกบิล' },
  { k: 'schedule', ic: '🗓️', t: 'ตารางงานหมอนวด' },
  { k: 'customer', ic: '👥', t: 'ลูกค้า' },
  { k: 'service', ic: '💆', t: 'บริการ & คอร์ส' },
  { k: 'therapist', ic: '🧑‍⚕️', t: 'หมอนวด / พนักงาน' },
  { k: 'revenue', ic: '💰', t: 'การเงิน' },
  { k: 'rooms', ic: '🚪', t: 'ห้องนวด' },
  { k: 'roles', ic: '🛡️', t: 'สิทธิ์การใช้งาน' },
  { k: 'report', ic: '📊', t: 'รายงาน' },
  { k: 'settings', ic: '⚙️', t: 'ตั้งค่า' },
]

export function TemplateShell({
  current, onNavigate, title, children,
}: {
  current: string
  onNavigate: (k: string) => void
  title: string
  children: ReactNode
}) {
  const user = useAuthStore(s => s.user)
  const [open, setOpen] = useState(false)

  return (
    <div className={`app${open ? ' side-open' : ''}`}>
      {/* Sidebar */}
      <aside className="side">
        <div className="side-brand">
          <div style={{ width: 38, height: 38, borderRadius: 11, background: 'var(--brand-grad)', display: 'grid', placeItems: 'center', fontSize: 20 }}>🌿</div>
          <div>
            <div style={{ color: '#fff', fontWeight: 800, fontSize: 15, lineHeight: 1.1 }}>Tharin</div>
            <div style={{ color: 'var(--side-text-dim)', fontSize: 11.5 }}>Massage & Spa</div>
          </div>
        </div>
        <nav style={{ flex: 1, overflowY: 'auto', padding: '6px 12px' }}>
          {NAV.map(n => (
            <a key={n.k}
              className={`nav-item${current === n.k ? ' active' : ''}`}
              onClick={() => { onNavigate(n.k); setOpen(false) }}
              style={{ cursor: 'pointer' }}>
              <span style={{ width: 22, textAlign: 'center' }}>{n.ic}</span>
              <span>{n.t}</span>
            </a>
          ))}
        </nav>
        <div className="side-user">
          <div style={{ width: 34, height: 34, borderRadius: '50%', overflow: 'hidden', background: '#2b3252', display: 'grid', placeItems: 'center', color: '#fff' }}>
            {user?.avatarUrl ? <img src={user.avatarUrl} alt="" style={{ width: '100%', height: '100%', objectFit: 'cover' }} /> : '👤'}
          </div>
          <div style={{ minWidth: 0 }}>
            <div style={{ color: '#fff', fontSize: 13, fontWeight: 600, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{user?.displayName ?? '—'}</div>
            <div style={{ color: 'var(--side-text-dim)', fontSize: 11 }}>เจ้าของร้าน</div>
          </div>
        </div>
      </aside>

      {/* Main */}
      <div className="main">
        <header className="topbar">
          <button className="icon-btn" onClick={() => setOpen(o => !o)} style={{ display: 'none' }} data-mobile-toggle>☰</button>
          <div style={{ fontWeight: 700, fontSize: 16, color: 'var(--ink)' }}>{title}</div>
          <div className="top-actions" style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 8 }}>
            <input placeholder="🔍 ค้นหา..." style={{ height: 36, border: '1px solid var(--line)', borderRadius: 10, padding: '0 12px', fontSize: 13, background: 'var(--card-2)' }} />
            <button className="icon-btn">🔔</button>
            <button className="icon-btn" onClick={() => onNavigate('__profile')} title="โปรไฟล์">
              {user?.avatarUrl ? <img src={user.avatarUrl} alt="" style={{ width: 28, height: 28, borderRadius: '50%', objectFit: 'cover' }} /> : '👤'}
            </button>
          </div>
        </header>
        <div className="content">{children}</div>
      </div>
    </div>
  )
}
