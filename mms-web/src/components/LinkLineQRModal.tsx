import { useEffect, useState } from 'react'
import { QRCodeSVG } from 'qrcode.react'
import { api } from '../api/client'

interface Props {
  userId: string
  userName: string
  onClose: () => void
}

export function LinkLineQRModal({ userId, userName, onClose }: Props) {
  const [liffUrl, setLiffUrl] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    api.post('/auth/link-token', { userId })
      .then(res => setLiffUrl(res.data.liffUrl))
      .catch(err => setError(err?.response?.data?.message ?? 'สร้าง QR ไม่สำเร็จ'))
      .finally(() => setLoading(false))
  }, [userId])

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div
        className="bg-white dark:bg-gray-800 rounded-2xl shadow-xl w-full max-w-sm p-6 text-center"
        onClick={e => e.stopPropagation()}
      >
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-bold text-gray-800 dark:text-white">ผูกบัญชี LINE</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl">✕</button>
        </div>

        <p className="text-sm text-gray-500 dark:text-gray-400 mb-4">
          ให้ <span className="font-semibold text-gray-800 dark:text-white">{userName}</span> สแกน QR นี้ด้วยแอป LINE เพื่อผูกบัญชี
        </p>

        {loading && <p className="text-sm text-gray-400 py-10">กำลังสร้าง QR...</p>}
        {error && <p className="text-sm text-red-500 py-10">{error}</p>}

        {liffUrl && (
          <>
            <div className="bg-white p-4 rounded-xl inline-block border border-gray-200">
              <QRCodeSVG value={liffUrl} size={200} level="M" />
            </div>
            <p className="text-xs text-gray-400 mt-4">QR ใช้ได้ 24 ชั่วโมง — สแกนแล้วผูกได้ครั้งเดียว</p>
            <div className="mt-3 bg-gray-50 dark:bg-gray-700 rounded-lg p-2">
              <p className="text-xs text-gray-400 mb-1">หรือเปิดลิงก์:</p>
              <a href={liffUrl} target="_blank" rel="noreferrer" className="text-xs text-emerald-600 break-all">{liffUrl}</a>
            </div>
          </>
        )}
      </div>
    </div>
  )
}
