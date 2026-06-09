import { useSignalRContext } from '../providers/SignalRProvider'
import { api } from '../api/client'

export function CleaningCheckModal() {
  const { cleaningCheckQueue, dismissCleaningCheck } = useSignalRContext()

  if (cleaningCheckQueue.length === 0) return null

  const current = cleaningCheckQueue[0]

  const handleDone = async () => {
    try {
      await api.post(`/room/${current.roomId}/cleaning-done`, {})
    } finally {
      dismissCleaningCheck(current.roomId)
    }
  }

  const handleExtend = async () => {
    try {
      await api.post(`/room/${current.roomId}/cleaning-extend`, {})
    } finally {
      dismissCleaningCheck(current.roomId)
    }
  }
  // const handleDone = async () => {
  //   try {
  //     await api.post(`/room/${current.roomId}/cleaning-done`, {})
  //     // ไม่ต้อง dismissCleaningCheck ที่นี่
  //     // รอ CleaningCheckDismissed broadcast จาก backend แทน
  //   } catch (err) {
  //     console.error('cleaning-done failed:', err)
  //     // ถ้า fail ไม่ปิด popup
  //   }
  // }

  // const handleExtend = async () => {
  //   try {
  //     await api.post(`/room/${current.roomId}/cleaning-extend`, {})
  //     // รอ CleaningCheckDismissed broadcast จาก backend แทน
  //   } catch (err) {
  //     console.error('cleaning-extend failed:', err)
  //   }
  // }
  return (
    <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center">
      <div className="bg-white dark:bg-gray-800 rounded-xl p-6 max-w-sm w-full mx-4 shadow-2xl">
        <h2 className="text-lg font-bold mb-2 dark:text-white">🧹 ตรวจสอบห้อง</h2>
        <p className="text-gray-600 dark:text-gray-300 mb-1">
          ห้อง <span className="font-semibold">{current.roomName}</span>
        </p>
        <p className="text-sm text-gray-500 dark:text-gray-400 mb-6">
          ทำความสะอาดเสร็จหรือยัง?
        </p>
        <div className="flex gap-3">
          <button onClick={handleDone} className="flex-1 bg-green-500 text-white py-2 rounded-lg font-medium">
            เสร็จแล้ว ✅
          </button>
          <button onClick={handleExtend} className="flex-1 bg-orange-400 text-white py-2 rounded-lg font-medium">
            ยังไม่เสร็จ 🔄
          </button>
        </div>
      </div>
    </div>
  )
}

export default CleaningCheckModal