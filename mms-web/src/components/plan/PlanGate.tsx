const planRank: Record<string, number> = {
  Free: 0, Basic: 1, Shop: 2, Business: 3,
}

const planName: Record<string, string> = {
  Basic: 'แผนพื้นฐาน', Shop: 'แผนร้านค้า', Business: 'แผนธุรกิจ',
}

interface PlanGateProps {
  required: 'Basic' | 'Shop' | 'Business'
  currentPlan: string
  children: React.ReactNode
  mode?: 'blur' | 'hidden' | 'disabled'
}

export function PlanGate({ required, currentPlan, children, mode = 'blur' }: PlanGateProps) {
  const hasAccess = (planRank[currentPlan] ?? 0) >= (planRank[required] ?? 99)

  if (hasAccess) return <>{children}</>

  if (mode === 'hidden') return null

  return (
    <div className="relative">
      <div className={`${mode === 'blur' ? 'filter blur-sm pointer-events-none select-none' : 'opacity-40 pointer-events-none'}`}>
        {children}
      </div>
      <div className="absolute inset-0 flex items-center justify-center bg-white/60 rounded-xl">
        <div className="text-center p-4">
          <p className="text-sm font-medium text-gray-700 mb-1">🔒 ต้องการ{planName[required]}</p>
          <button className="mt-2 text-xs bg-violet-600 hover:bg-violet-700 text-white px-4 py-1.5 rounded-full transition">
            ดูรายละเอียดแผน
          </button>
        </div>
      </div>
    </div>
  )
}
