import { useEffect, useState } from 'react'
import { api } from '../api/client'
import type { DashboardSnapshot } from '../types/dashboard'
import { TherapistTimeline } from '../components/TherapistTimeline'

const baht = (n: number) => (n ?? 0).toLocaleString('th-TH')

export function DashboardTemplate({ onNavigate }: { onNavigate: (k: string) => void }) {
  const [snap, setSnap] = useState<DashboardSnapshot | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    api.get('/dashboard').then(r => setSnap(r.data)).catch(() => {}).finally(() => setLoading(false))
  }, [])

  if (loading) return <p style={{ textAlign: 'center', color: 'var(--ink-3)', padding: 40 }}>กำลังโหลด...</p>
  if (!snap) return <p style={{ textAlign: 'center', color: 'var(--red)', padding: 40 }}>โหลดข้อมูลไม่สำเร็จ</p>

  const q = snap.queue
  const total = q.totalToday || 1
  const segs = [
    { label: 'เสร็จสิ้น', val: q.completed, color: '#22b07d' },
    { label: 'กำลังให้บริการ', val: q.inService, color: '#3b9bff' },
    { label: 'รอดำเนินการ', val: q.waiting, color: '#f5a623' },
    { label: 'ยกเลิก', val: q.cancelled, color: '#c2c7d6' },
  ]
  // donut conic-gradient
  let acc = 0
  const stops = segs.map(s => {
    const from = (acc / total) * 360; acc += s.val; const to = (acc / total) * 360
    return `${s.color} ${from}deg ${to}deg`
  }).join(',')

  return (
    <div className="layout-2">
      {/* MAIN */}
      <div>
        {/* stat cards */}
        <div className="stat-grid">
          <Stat icon="💰" cls="ic-purple" label="รายรับวันนี้" val={baht(snap.revenue.totalRevenue)} unit="บาท" sub={`จาก ${snap.revenue.totalReceipts} บิล`} trend={`${snap.revenue.totalReceipts} บิล`} onClick={() => onNavigate('revenue')} />
          <Stat icon="📈" cls="ic-green" label="รายรับเดือนนี้" val={baht(snap.monthlyRevenue.totalRevenue)} unit="บาท" sub={`จาก ${snap.monthlyRevenue.totalReceipts} บิล`} trend={`${snap.monthlyRevenue.totalReceipts} บิล`} onClick={() => onNavigate('revenue')} />
          <Stat icon="👥" cls="ic-orange" label="ลูกค้าวันนี้" val={String(q.totalToday)} unit="คน" sub={`รอคิว ${q.waiting} คน`} trend={q.waiting > 0 ? `รอ ${q.waiting}` : undefined} onClick={() => onNavigate('schedule')} />
          <Stat icon="📅" cls="ic-blue" label="การจองวันนี้" val={String(snap.bookings.total)} unit="การจอง" sub={`รอดำเนินการ ${snap.bookings.pending}`} trend={snap.bookings.pending > 0 ? `ใหม่ ${snap.bookings.pending}` : undefined} onClick={() => onNavigate('booking')} />
        </div>

        {/* donut + finance */}
        <div className="grid-2 mb24">
          <div className="card card-pad">
            <div className="card-h"><h3>ภาพรวมการจองวันนี้</h3></div>
            <div className="donut-wrap" style={{ display: 'flex', gap: 20, alignItems: 'center', flexWrap: 'wrap' }}>
              <div className="donut" style={{ width: 130, height: 130, borderRadius: '50%', background: `conic-gradient(${stops})`, display: 'grid', placeItems: 'center', position: 'relative' }}>
                <div style={{ width: 86, height: 86, borderRadius: '50%', background: '#fff', display: 'grid', placeItems: 'center', textAlign: 'center' }}>
                  <div><b style={{ fontSize: 22 }}>{q.totalToday}</b><div style={{ fontSize: 11, color: 'var(--ink-3)' }}>ทั้งหมด</div></div>
                </div>
              </div>
              <div style={{ flex: 1, minWidth: 160 }}>
                {segs.map(s => (
                  <div key={s.label} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '4px 0', fontSize: 13 }}>
                    <span style={{ width: 10, height: 10, borderRadius: 3, background: s.color }} />
                    <span style={{ flex: 1, color: 'var(--ink-2)' }}>{s.label}</span>
                    <b>{s.val}</b>
                    <span style={{ color: 'var(--ink-3)', width: 44, textAlign: 'right' }}>{((s.val / total) * 100).toFixed(1)}%</span>
                  </div>
                ))}
              </div>
            </div>
          </div>

          <div className="card card-pad">
            <div className="card-h"><h3>รายรับ (เดือนนี้)</h3></div>
            <table className="tbl" style={{ width: '100%' }}>
              <thead><tr><th>วิธีชำระ</th><th style={{ textAlign: 'right' }}>จำนวนเงิน (บาท)</th></tr></thead>
              <tbody>
                {snap.revenue.byMethod.length === 0 && <tr><td colSpan={2} style={{ textAlign: 'center', color: 'var(--ink-3)', padding: 12 }}>ยังไม่มีรายการ</td></tr>}
                {snap.revenue.byMethod.map(m => (
                  <tr key={m.method}><td>{methodLabel(m.method)}</td><td style={{ textAlign: 'right' }}>{m.amount.toLocaleString('th-TH', { minimumFractionDigits: 2 })}</td></tr>
                ))}
                <tr><td style={{ fontWeight: 700, color: 'var(--brand)' }}>รายรับรวมเดือนนี้</td><td style={{ textAlign: 'right', fontWeight: 700, color: 'var(--brand)' }}>{snap.monthlyRevenue.totalRevenue.toLocaleString('th-TH', { minimumFractionDigits: 2 })}</td></tr>
              </tbody>
            </table>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginTop: 10 }}>
              <div style={{ flex: 1, height: 6, borderRadius: 4, background: 'var(--line)', overflow: 'hidden' }}>
                <div style={{ width: '100%', height: '100%', background: 'var(--brand-grad)' }} />
              </div>
              <button className="btn btn-soft btn-sm" onClick={() => onNavigate('revenue')}>ดูรายงานการเงิน</button>
            </div>
          </div>
        </div>

        {/* schedule gantt timeline */}
        <div className="card card-pad">
          <div className="card-h"><h3>ตารางงานหมอนวดวันนี้</h3></div>
          <div style={{ marginTop: 8 }}>
            <TherapistTimeline refreshKey={0} />
          </div>
        </div>
      </div>

      {/* RIGHT COL */}
      <div className="rightcol">
        <div className="widget">
          <div className="widget-h"><h3>คิวที่รอดำเนินการ</h3>{q.waiting > 0 && <span className="badge b-red">{q.waiting}</span>}</div>
          <div style={{ padding: 4 }}>
            {q.waitingList.length === 0 ? <p style={{ color: 'var(--ink-3)', fontSize: 13, textAlign: 'center', padding: 12 }}>ไม่มีคิวรอ</p> :
              q.waitingList.slice(0, 6).map((w: any) => (
                <div key={w.id} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '8px 6px', borderBottom: '1px solid var(--line)' }}>
                  <div style={{ width: 30, height: 30, borderRadius: '50%', background: '#eef0f6', display: 'grid', placeItems: 'center' }}>👤</div>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: 13, fontWeight: 600 }}>{w.customer?.displayName ?? '-'}</div>
                    <div style={{ fontSize: 11, color: 'var(--ink-3)' }}>{w.serviceCount} บริการ</div>
                  </div>
                </div>
              ))}
            <button className="btn btn-pri" style={{ width: '100%', marginTop: 8 }} onClick={() => onNavigate('schedule')}>จัดการคิวทั้งหมด</button>
          </div>
        </div>

        <div className="widget">
          <div className="widget-h"><h3>สถานะร้าน</h3></div>
          <div style={{ padding: 8, fontSize: 13 }}>
            <StatusRow label="หมอนวดว่าง" val={`${snap.therapists.available} คน`} />
            <StatusRow label="กำลังบริการ" val={`${q.inService} คิว`} />
            <StatusRow label="ห้องว่าง" val={`${snap.rooms.available}/${snap.rooms.total}`} />
            <StatusRow label="เสร็จวันนี้" val={`${q.completed} คิว`} />
          </div>
        </div>
      </div>
    </div>
  )
}

function Stat({ icon, cls, label, val, unit, sub, trend, onClick }: any) {
  return (
    <div className="stat" style={{ cursor: 'pointer' }} onClick={onClick}>
      <div className="row1"><div className={`ic ${cls}`}>{icon}</div><span className="tt">{label}</span></div>
      <div className="val">{val} <small>{unit}</small></div>
      <div className="row3"><span className="from">{sub}</span>{trend && <span className="trend up">{trend}</span>}</div>
    </div>
  )
}
function StatusRow({ label, val }: { label: string; val: string }) {
  return <div style={{ display: 'flex', justifyContent: 'space-between', padding: '5px 4px', borderBottom: '1px solid var(--line)' }}><span style={{ color: 'var(--ink-2)' }}>{label}</span><b>{val}</b></div>
}
function methodLabel(m: string) { return ({ Cash: 'เงินสด', Transfer: 'โอน', PromptPay: 'พร้อมเพย์', QR: 'พร้อมเพย์', Card: 'บัตร' } as any)[m] ?? m }
function therStatus(s: number) { return ['ว่าง', 'ไม่ว่าง', 'พัก', 'ลา', 'หยุด', 'ออฟไลน์'][s] ?? '-' }
