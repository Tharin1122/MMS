import { useEffect, useState } from 'react'
import { api } from '../api/client'
import type { DashboardSnapshot } from '../types/dashboard'
import { TherapistTimeline } from '../components/TherapistTimeline'
import { DayBookingsModal } from '../components/DayBookingsModal'

const baht = (n: number) => (n ?? 0).toLocaleString('th-TH')
const baht2 = (n: number) => (n ?? 0).toLocaleString('th-TH', { minimumFractionDigits: 2 })
const methodLabel = (m: string) => (({ Cash: 'เงินสด', Transfer: 'โอน', PromptPay: 'พร้อมเพย์', QR: 'พร้อมเพย์', Card: 'บัตร' } as any)[m] ?? m)

export function DashboardTemplate({ onNavigate }: { onNavigate: (k: string) => void }) {
  const [snap, setSnap] = useState<DashboardSnapshot | null>(null)
  const [loading, setLoading] = useState(true)
  const [calDate, setCalDate] = useState<string | null>(null)

  useEffect(() => { api.get('/dashboard').then(r => setSnap(r.data)).catch(() => {}).finally(() => setLoading(false)) }, [])

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
  let acc = 0
  const stops = segs.map(s => { const f = (acc / total) * 360; acc += s.val; const t = (acc / total) * 360; return `${s.color} ${f}deg ${t}deg` }).join(',')

  const mRev = snap.monthlyRevenue.totalRevenue
  const expense = 0                 // ยังไม่มีโมดูลรายจ่าย → 0 จริง
  const profit = mRev - expense
  const margin = mRev > 0 ? Math.round((profit / mRev) * 100) : 0

  return (
    <div className="layout-2">
      {/* MAIN */}
      <div>
        {/* stat cards */}
        <div className="stat-grid">
          <div className="stat" style={{ cursor: 'pointer' }} onClick={() => onNavigate('revenue')}>
            <div className="row1"><div className="ic ic-purple">💰</div><span className="tt">รายรับวันนี้</span></div>
            <div className="val">{baht(snap.revenue.totalRevenue)} <small>บาท</small></div>
            <div className="row3"><span className="from">จาก {snap.revenue.totalReceipts} บิล</span></div>
          </div>
          <div className="stat" style={{ cursor: 'pointer' }} onClick={() => onNavigate('revenue')}>
            <div className="row1"><div className="ic ic-green">📈</div><span className="tt">รายรับเดือนนี้</span></div>
            <div className="val">{baht(mRev)} <small>บาท</small></div>
            <div className="row3"><span className="from">จาก {snap.monthlyRevenue.totalReceipts} บิล</span></div>
          </div>
          <div className="stat" style={{ cursor: 'pointer' }} onClick={() => onNavigate('schedule')}>
            <div className="row1"><div className="ic ic-orange">👥</div><span className="tt">ลูกค้าวันนี้</span></div>
            <div className="val">{q.totalToday} <small>คน</small></div>
            <div className="row3"><span className="from">รอคิว {q.waiting} คน</span></div>
          </div>
          <div className="stat" style={{ cursor: 'pointer' }} onClick={() => onNavigate('booking')}>
            <div className="row1"><div className="ic ic-blue">📅</div><span className="tt">การจองวันนี้</span></div>
            <div className="val">{snap.bookings.total} <small>การจอง</small></div>
            <div className="row3"><span className="from">รอดำเนินการ {snap.bookings.pending}</span></div>
          </div>
        </div>

        {/* donut + finance */}
        <div className="grid-2 mb24">
          <div className="card card-pad">
            <div className="card-h"><h3>ภาพรวมการจองวันนี้</h3></div>
            <div className="donut-wrap">
              <div className="donut" style={{ background: `conic-gradient(${stops})` }}>
                <div className="ctr"><b>{q.totalToday}</b><span>ทั้งหมด</span></div>
              </div>
              <div className="legend">
                {segs.map(s => (
                  <div className="lg" key={s.label}>
                    <span className="d" style={{ background: s.color }} />
                    <span className="nm">{s.label}</span>
                    <span className="vl">{s.val}</span>
                    <span className="pct">{((s.val / total) * 100).toFixed(1)}%</span>
                  </div>
                ))}
              </div>
            </div>
          </div>

          <div className="card card-pad">
            <div className="card-h"><h3>รายรับ - รายจ่าย (เดือนนี้)</h3></div>
            <table className="tbl" style={{ marginTop: -4 }}>
              <thead><tr><th>รายการ</th><th style={{ textAlign: 'right' }}>จำนวนเงิน (บาท)</th></tr></thead>
              <tbody>
                <tr><td>รายรับรวม</td><td style={{ textAlign: 'right', fontWeight: 700 }} className="t-green">{baht2(mRev)}</td></tr>
                <tr><td>รายจ่ายรวม</td><td style={{ textAlign: 'right', fontWeight: 700 }} className="t-red">{baht2(expense)}</td></tr>
                <tr><td style={{ fontWeight: 700, color: 'var(--brand)' }}>กำไรสุทธิ</td><td style={{ textAlign: 'right', fontWeight: 700, fontSize: 16, color: 'var(--brand)' }}>{baht2(profit)}</td></tr>
              </tbody>
            </table>
            <div className="flex jb ac" style={{ marginTop: 8 }}>
              <div className="prog" style={{ flex: 1, marginRight: 16 }}><i style={{ width: `${margin}%` }} /></div>
              <button className="btn btn-soft btn-sm" onClick={() => onNavigate('revenue')}>ดูรายงานการเงิน</button>
            </div>
          </div>
        </div>

        {/* schedule timeline */}
        <div className="card card-pad mb24">
          <div className="card-h">
            <h3>ตารางงานหมอนวดวันนี้</h3>
            <div className="sp flex g12 ac"><button className="btn btn-ghost btn-sm" onClick={() => onNavigate('schedule')}>ดูตารางเต็ม</button></div>
          </div>
          <TherapistTimeline refreshKey={0} />
        </div>

        {/* quick actions */}
        <div className="quick">
          <a onClick={() => onNavigate('booking')}><span className="qi" style={{ background: 'var(--brand-soft)', color: 'var(--brand)' }}>📅</span><div><div className="qt">สร้างการจอง</div><div className="qs">เพิ่มการจองใหม่</div></div></a>
          <a onClick={() => onNavigate('customer')}><span className="qi" style={{ background: 'var(--green-soft)', color: '#178a61' }}>👥</span><div><div className="qt">ลูกค้าใหม่</div><div className="qs">เพิ่มลูกค้า</div></div></a>
          <a onClick={() => onNavigate('schedule')}><span className="qi" style={{ background: 'var(--orange-soft)', color: '#b9791a' }}>🎫</span><div><div className="qt">จัดการคิว</div><div className="qs">คิวรอ / กำลังให้บริการ</div></div></a>
          <a onClick={() => onNavigate('revenue')}><span className="qi" style={{ background: 'var(--blue-soft)', color: '#2477cc' }}>💵</span><div><div className="qt">รายงานการเงิน</div><div className="qs">รายรับ - รายจ่าย</div></div></a>
          <a onClick={() => onNavigate('service')}><span className="qi" style={{ background: '#f3eafe', color: '#8b5cf6' }}>💆</span><div><div className="qt">จัดการคอร์ส</div><div className="qs">คอร์ส & แพ็กเกจ</div></div></a>
          <a onClick={() => onNavigate('therapist')}><span className="qi" style={{ background: '#eef0f6', color: '#6b7290' }}>🧑‍⚕️</span><div><div className="qt">หมอนวด</div><div className="qs">จัดการพนักงาน</div></div></a>
        </div>
      </div>

      {/* RIGHT COLUMN */}
      <div className="rightcol">
        <div className="widget">
          <div className="widget-h"><h3>คิวที่รอดำเนินการ</h3>{q.waiting > 0 && <span className="sp badge b-red">{q.waiting} รายการ</span>}</div>
          <div id="queue">
            {q.waitingList.length === 0 ? <p style={{ color: 'var(--ink-3)', fontSize: 13, textAlign: 'center', padding: 12 }}>ไม่มีคิวรอ</p> :
              q.waitingList.slice(0, 6).map((w: any) => (
                <div key={w.id} className="qrow" style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '9px 4px', borderBottom: '1px solid var(--line)' }}>
                  <div style={{ width: 32, height: 32, borderRadius: '50%', background: '#eef0f6', display: 'grid', placeItems: 'center' }}>👤</div>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: 13, fontWeight: 600 }}>{w.customer?.displayName ?? '-'}</div>
                    <div style={{ fontSize: 11, color: 'var(--ink-3)' }}>{w.serviceCount} บริการ</div>
                  </div>
                  <span className="badge b-orange">รอคิว</span>
                </div>
              ))}
          </div>
          <button className="btn btn-pri" style={{ width: '100%', marginTop: 14 }} onClick={() => onNavigate('schedule')}>จัดการคิวทั้งหมด</button>
        </div>

        <div className="widget">
          <div className="widget-h"><h3>ปฏิทินการจอง</h3></div>
          <MiniCal onSelect={setCalDate} />
        </div>

        <div className="widget">
          <div className="widget-h"><h3>สถานะร้าน</h3></div>
          <div style={{ padding: '4px 2px', fontSize: 13 }}>
            {snap.revenue.byMethod.map(m => (
              <div key={m.method} style={{ display: 'flex', justifyContent: 'space-between', padding: '5px 4px', borderBottom: '1px solid var(--line)' }}><span style={{ color: 'var(--ink-2)' }}>{methodLabel(m.method)} ({m.count})</span><b>฿{baht(m.amount)}</b></div>
            ))}
            <div style={{ display: 'flex', justifyContent: 'space-between', padding: '5px 4px' }}><span style={{ color: 'var(--ink-2)' }}>หมอนวดว่าง</span><b>{snap.therapists.available} คน</b></div>
            <div style={{ display: 'flex', justifyContent: 'space-between', padding: '5px 4px' }}><span style={{ color: 'var(--ink-2)' }}>ห้องว่าง</span><b>{snap.rooms.available}/{snap.rooms.total}</b></div>
          </div>
        </div>
      </div>

      {calDate && <DayBookingsModal date={calDate} onClose={() => setCalDate(null)} onGoToBookings={() => { setCalDate(null); onNavigate('booking') }} />}
    </div>
  )
}

function MiniCal({ onSelect }: { onSelect: (d: string) => void }) {
  const now = new Date()
  const y = now.getFullYear(), m = now.getMonth(), today = now.getDate()
  const firstDay = new Date(y, m, 1).getDay()
  const days = new Date(y, m + 1, 0).getDate()
  const pad = (n: number) => String(n).padStart(2, '0')
  const dow = ['อา', 'จ', 'อ', 'พ', 'พฤ', 'ศ', 'ส']
  return (
    <div className="mcal" style={{ padding: 4 }}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7,1fr)', gap: 2, textAlign: 'center' }}>
        {dow.map(d => <div key={d} style={{ fontSize: 11, color: 'var(--ink-3)', padding: '2px 0' }}>{d}</div>)}
        {Array.from({ length: firstDay }, (_, i) => <div key={'e' + i} />)}
        {Array.from({ length: days }, (_, i) => {
          const d = i + 1
          return <button key={d} onClick={() => onSelect(`${y}-${pad(m + 1)}-${pad(d)}`)}
            style={{ fontSize: 12, padding: '6px 0', borderRadius: 8, border: 'none', cursor: 'pointer', background: d === today ? 'var(--brand)' : 'transparent', color: d === today ? '#fff' : 'var(--ink-2)', fontWeight: d === today ? 700 : 400 }}>{d}</button>
        })}
      </div>
      <div style={{ fontSize: 11, color: 'var(--ink-3)', textAlign: 'center', marginTop: 6 }}>คลิกวันที่เพื่อดูคิว</div>
    </div>
  )
}
