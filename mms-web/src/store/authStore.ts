import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { AuthState, User } from '../types'

interface AuthStore extends AuthState {
  login: (accessToken: string, refreshToken: string, user: User, permissions: string[]) => void
  logout: () => void
  hasPermission: (code: string) => boolean
  refreshAccessToken: () => Promise<boolean>
}

export const useAuthStore = create<AuthStore>()(
  persist(
    (set, get) => ({
      accessToken: null,
      refreshToken: null,
      user: null,
      permissions: [],

      login: (accessToken, refreshToken, user, permissions) => {
        localStorage.setItem('accessToken', accessToken)
        localStorage.setItem('refreshToken', refreshToken)
        set({ accessToken, refreshToken, user, permissions })
      },

      logout: () => {
        localStorage.clear()
        set({ accessToken: null, refreshToken: null, user: null, permissions: [] })
      },

      hasPermission: (code) => get().permissions.includes(code),

      // เรียก /api/auth/refresh แล้วอัปเดต token ใหม่
      refreshAccessToken: async () => {
        const { refreshToken } = get()
        if (!refreshToken) return false
        try {
          const apiBase = import.meta.env.VITE_API_URL
            ?? (import.meta.env.PROD ? 'https://mms-api-25xj.onrender.com/api' : 'http://localhost:5065/api')
          const res = await fetch(`${apiBase}/auth/refresh`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ refreshToken }),
          })
          if (!res.ok) {
            get().logout()
            return false
          }
          const data = await res.json()
          localStorage.setItem('accessToken', data.accessToken)
          localStorage.setItem('refreshToken', data.refreshToken)
          set({ accessToken: data.accessToken, refreshToken: data.refreshToken })
          return true
        } catch {
          get().logout()
          return false
        }
      }
    }),
    { name: 'mms-auth' }
  )
)
