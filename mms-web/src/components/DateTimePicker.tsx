// แปลงเวลาท้องถิ่น "YYYY-MM-DDTHH:mm" → UTC ISO (มี Z) สำหรับส่งไป backend
export const localToUtcIso = (local: string) => {
  if (!local) return ''
  const d = new Date(local)   // parse เป็นเวลาท้องถิ่นของ browser
  return isNaN(d.getTime()) ? local : d.toISOString()
}

// Date + 24h time picker (กัน AM/PM) — value = "YYYY-MM-DDTHH:mm"
export function DateTimePicker({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  const [datePart, timePart] = (value || 'T00:00').split('T')
  const [hh = '00', mm = '00'] = (timePart || '00:00').split(':')

  const setDate = (d: string) => onChange(`${d}T${hh}:${mm}`)
  const setHH = (h: string) => onChange(`${datePart}T${h}:${mm}`)
  const setMM = (m: string) => onChange(`${datePart}T${hh}:${m}`)

  const cls = "text-sm border border-gray-300 dark:border-gray-600 rounded-lg px-2 py-1.5 bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-violet-300"

  return (
    <div className="flex items-center gap-1">
      <input type="date" value={datePart} onChange={e => setDate(e.target.value)} className={cls} />
      <select value={hh} onChange={e => setHH(e.target.value)} className={cls}>
        {Array.from({ length: 24 }, (_, i) => String(i).padStart(2, '0')).map(h => <option key={h} value={h}>{h}</option>)}
      </select>
      <span className="text-gray-400">:</span>
      <select value={mm} onChange={e => setMM(e.target.value)} className={cls}>
        {Array.from({ length: 60 }, (_, i) => String(i).padStart(2, '0')).map(m => <option key={m} value={m}>{m}</option>)}
      </select>
      <span className="text-xs text-gray-400 ml-0.5">น.</span>
    </div>
  )
}
