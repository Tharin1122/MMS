import axios from 'axios'
import { useAuthStore } from '../store/authStore'

export const api = axios.create({
  baseURL: 'http://localhost:5065/api',
})

// ── Request interceptor — ใส่ token ทุก request ──
api.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

// ── Response interceptor — refresh token อัตโนมัติเมื่อได้ 401 ──
let isRefreshing = false
let pendingQueue: { resolve: (token: string) => void; reject: () => void }[] = []

api.interceptors.response.use(
  res => res,
  async (error) => {
    const original = error.config

    // ถ้า 401 และยังไม่ได้ retry
    if (error.response?.status === 401 && !original._retry) {
      original._retry = true

      if (isRefreshing) {
        // รอ token ใหม่จาก queue
        return new Promise((resolve, reject) => {
          pendingQueue.push({
            resolve: (token) => {
              original.headers.Authorization = `Bearer ${token}`
              resolve(api(original))
            },
            reject: () => reject(error)
          })
        })
      }

      isRefreshing = true

      const ok = await useAuthStore.getState().refreshAccessToken()

      isRefreshing = false

      if (ok) {
        const newToken = useAuthStore.getState().accessToken!
        // ส่ง request ที่ค้างไว้ทั้งหมด
        pendingQueue.forEach(p => p.resolve(newToken))
        pendingQueue = []
        // retry original request
        original.headers.Authorization = `Bearer ${newToken}`
        return api(original)
      } else {
        // refresh ไม่สำเร็จ → logout
        pendingQueue.forEach(p => p.reject())
        pendingQueue = []
        useAuthStore.getState().logout()
        return Promise.reject(error)
      }
    }

    return Promise.reject(error)
  }
)
