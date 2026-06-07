export const th = {
  // Auth
  'auth.login.title': 'เข้าสู่ระบบ MMS',
  'auth.login.subtitle': 'ระบบจัดการร้านนวด',
  'auth.login.line': 'เข้าสู่ระบบด้วย LINE',
  'auth.login.dev': 'ทดสอบ (Dev)',
  'auth.logout': 'ออกจากระบบ',

  // Dashboard
  'dashboard.title': 'ภาพรวมสาขา',
  'dashboard.today': 'วันนี้',
  'dashboard.revenue': 'รายได้วันนี้',
  'dashboard.customers': 'ลูกค้าวันนี้',
  'dashboard.therapist.available': 'ว่าง',
  'dashboard.therapist.occupied': 'กำลังนวด',
  'dashboard.therapist.break': 'พักผ่อน',
  'dashboard.therapist.leave': 'ลา',
  'dashboard.therapist.offline': 'ออฟไลน์',
  'dashboard.room.available': 'ว่าง',
  'dashboard.room.occupied': 'ใช้งาน',
  'dashboard.room.cleaning': 'กำลังทำความสะอาด',
  'dashboard.room.maintenance': 'ซ่อมบำรุง',

  // Queue
  'queue.title': 'คิวรอบริการ',
  'queue.waiting': 'รอคิว',
  'queue.inService': 'กำลังบริการ',
  'queue.completed': 'เสร็จแล้ว',
  'queue.estimatedWait': 'รอประมาณ',
  'queue.minutes': 'นาที',
  'queue.noWaiting': 'ไม่มีคิวรอ',

  // Common
  'common.total': 'ทั้งหมด',
  'common.loading': 'กำลังโหลด...',
  'common.error': 'เกิดข้อผิดพลาด',
  'common.retry': 'ลองใหม่',
  'common.baht': 'บาท',
  'common.darkMode': 'โหมดมืด',
  'common.lightMode': 'โหมดสว่าง',
} as const

export type TranslationKey = keyof typeof th

export function t(key: TranslationKey): string {
  return th[key] ?? key
}