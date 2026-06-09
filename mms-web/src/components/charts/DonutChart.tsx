interface Segment {
  label: string
  value: number
  color: string
}

interface DonutChartProps {
  segments: Segment[]
  total: number
  centerLabel?: string
  size?: number
  strokeWidth?: number
}

export function DonutChart({
  segments,
  total,
  centerLabel,
  size = 160,
  strokeWidth = 28,
}: DonutChartProps) {
  const r = (size - strokeWidth) / 2
  const circumference = 2 * Math.PI * r
  const cx = size / 2
  const cy = size / 2

  let offset = 0
  const arcs = segments.map(seg => {
    const pct = total > 0 ? seg.value / total : 0
    const dash = pct * circumference
    const gap = circumference - dash
    const arc = { ...seg, dash, gap, offset }
    offset += dash
    return arc
  })

  return (
    <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} style={{ transform: 'rotate(-90deg)' }}>
      {/* background track */}
      <circle
        cx={cx} cy={cy} r={r}
        fill="none"
        stroke="#e5e7eb"
        strokeWidth={strokeWidth}
      />
      {arcs.map((arc, i) => (
        arc.value > 0 && (
          <circle
            key={i}
            cx={cx} cy={cy} r={r}
            fill="none"
            stroke={arc.color}
            strokeWidth={strokeWidth}
            strokeDasharray={`${arc.dash} ${arc.gap}`}
            strokeDashoffset={-arc.offset}
            strokeLinecap="butt"
          />
        )
      ))}
      {centerLabel && (
        <g style={{ transform: `rotate(90deg) translate(0, -${size}px)` }}>
          <text
            x={cx} y={cy}
            textAnchor="middle"
            dominantBaseline="middle"
            style={{ fontSize: 14, fontWeight: 700, fill: '#374151' }}
          >
            {centerLabel}
          </text>
        </g>
      )}
    </svg>
  )
}
