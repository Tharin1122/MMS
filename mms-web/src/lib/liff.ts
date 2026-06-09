import liff from '@line/liff'

const LIFF_ID = import.meta.env.VITE_LIFF_ID ?? ''

let initialized = false

/** Init LIFF ครั้งเดียว */
export async function initLiff(): Promise<boolean> {
  if (!LIFF_ID) return false
  if (initialized) return true
  await liff.init({ liffId: LIFF_ID })
  initialized = true
  return true
}

/** เปิด LINE login แล้วคืน access token (ใช้ส่งให้ backend verify) */
export async function lineLogin(): Promise<string | null> {
  const ok = await initLiff()
  if (!ok) throw new Error('ยังไม่ได้ตั้งค่า LIFF_ID')

  if (!liff.isLoggedIn()) {
    liff.login({ redirectUri: window.location.href })
    return null // จะ redirect ออกไป LINE — กลับมาแล้วค่อยดึง token
  }

  return liff.getAccessToken()
}

/** เช็คว่ามี LIFF config ไหม (ใช้ซ่อน/แสดงปุ่ม) */
export const isLiffConfigured = () => !!LIFF_ID

export { liff }
