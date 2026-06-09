export const th = {
  // Auth
  'auth.login.title': 'เข้าสู่ระบบ MMS',
  'auth.login.subtitle': 'ระบบจัดการร้านนวด',
  'auth.login.line': 'เข้าสู่ระบบด้วย LINE',
  'auth.login.dev': 'ทดสอบ (Dev)',
  'auth.logout': 'ออกจากระบบ',

  // Nav
  'nav.dashboard': 'แดชบอร์ด',
  'nav.booking': 'การจอง & คิวงาน',
  'nav.schedule': 'ตารางงานหมอนวด',
  'nav.customer': 'ลูกค้า',
  'nav.service': 'บริการ & คอร์ส',
  'nav.therapist': 'หมอนวด / พนักงาน',
  'nav.finance': 'การเงิน',
  'nav.revenue': 'รายรับ – รายจ่าย',
  'nav.promotion': 'แพ็กเกจ & โปรโมชั่น',
  'nav.stock': 'สต็อกสินค้า',
  'nav.roles': 'สิทธิ์การใช้งาน',
  'nav.settings': 'ตั้งค่า',
  'nav.report': 'รายงาน',
  'nav.logs': 'Logs & กิจกรรม',

  // Dashboard
  'dashboard.title': 'แดชบอร์ด',
  'dashboard.today': 'วันนี้',
  'dashboard.todayRevenue': 'รายรับวันนี้',
  'dashboard.monthRevenue': 'รายรับเดือนนี้',
  'dashboard.todayCustomer': 'ลูกค้าวันนี้',
  'dashboard.todayBooking': 'การจองวันนี้',
  'dashboard.revenue': 'รายได้วันนี้',
  'dashboard.customers': 'ลูกค้าวันนี้',
  'dashboard.bookingOverview': 'ภาพรวมการจองวันนี้',
  'dashboard.revenueReport': 'รายรับ – รายจ่าย (เดือนนี้)',
  'dashboard.therapistTimeline': 'ตารางงานหมอนวดวันนี้',
  'dashboard.queueWaiting': 'คิวรอดำเนินการ (เรียลไทม์)',
  'dashboard.notifications': 'การแจ้งเตือน',
  'dashboard.calendar': 'ปฏิทินการจอง',
  'dashboard.fromOrders': 'จาก {n} การจอง',
  'dashboard.fromCustomers': 'ลูกค้าใหม่ {n} คน',
  'dashboard.pending': 'รอดำเนินการ {n}',
  'dashboard.viewReport': 'ดูรายงานการเงิน',
  'dashboard.manageQueue': 'จัดการคิวทั้งหมด',
  'dashboard.broadcast': 'รีเฟรชทุกหน้าจอ',

  // Booking Status
  'booking.status.pending': 'รอยืนยัน',
  'booking.status.confirmed': 'ยืนยันแล้ว',
  'booking.status.inProgress': 'กำลังให้บริการ',
  'booking.status.completed': 'เสร็จสิ้น',
  'booking.status.cancelled': 'ยกเลิก',
  'booking.status.noShow': 'ไม่มาตามนัด',

  // Therapist Status
  'therapist.available': 'ว่าง',
  'therapist.occupied': 'กำลังนวด',
  'therapist.break': 'พักผ่อน',
  'therapist.leave': 'ลา',
  'therapist.offDuty': 'ออฟดิวตี้',
  'therapist.offline': 'ออฟไลน์',
  'therapist.online': 'ออนไลน์',

  // Room Status
  'room.available': 'ว่าง',
  'room.occupied': 'ใช้งาน',
  'room.cleaning': 'ทำความสะอาด',
  'room.maintenance': 'ซ่อมบำรุง',
  'room.closed': 'ปิด',

  // Revenue
  'revenue.income': 'รายรับรวม',
  'revenue.expense': 'รายจ่ายรวม',
  'revenue.profit': 'กำไรสุทธิ',
  'revenue.cash': 'เงินสด',
  'revenue.transfer': 'โอนเงิน',
  'revenue.qr': 'QR / พร้อมเพย์',
  'revenue.card': 'บัตรเครดิต',

  // Queue
  'queue.title': 'คิวรอบริการ',
  'queue.waiting': 'รอคิว',
  'queue.inService': 'กำลังบริการ',
  'queue.completed': 'เสร็จแล้ว',
  'queue.estimatedWait': 'รอประมาณ',
  'queue.minutes': 'นาที',
  'queue.noWaiting': 'ไม่มีคิวรอ',
  'queue.overdue': 'เกินเวลา',
  'queue.onTime': 'ตรงเวลา',

  // Plan
  'plan.free': 'ฟรีตลอด',
  'plan.basic': 'แผนพื้นฐาน',
  'plan.shop': 'แผนร้านค้า',
  'plan.business': 'แผนธุรกิจ',
  'plan.upgrade': 'อัปเกรดแผน',
  'plan.locked': 'ฟีเจอร์นี้ต้องการแผน',
  'plan.trialLeft': 'ทดลองใช้เหลืออีก {n} วัน',

  // Common
  'common.total': 'ทั้งหมด',
  'common.loading': 'กำลังโหลด...',
  'common.error': 'เกิดข้อผิดพลาด',
  'common.retry': 'ลองใหม่',
  'common.baht': 'บาท',
  'common.darkMode': 'โหมดมืด',
  'common.lightMode': 'โหมดสว่าง',
  'common.today': 'วันนี้',
  'common.viewAll': 'ดูทั้งหมด',
  'common.noData': 'ไม่มีข้อมูล',
  'common.person': 'คน',
  'common.order': 'การจอง',
  'common.search': 'ค้นหาข้อมูล...',
  'common.createBooking': 'สร้างการจอง',
} as const

export type TranslationKey = keyof typeof th

export function t(key: TranslationKey, params?: Record<string, string | number>): string {
  let str: string = th[key] ?? key
  if (params) {
    Object.entries(params).forEach(([k, v]) => {
      str = str.replace(`{${k}}`, String(v))
    })
  }
  return str
}
