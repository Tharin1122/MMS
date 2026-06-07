import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { AuthState, User } from '../types'

interface AuthStore extends AuthState {
  login: (accessToken: string, refreshToken: string, user: User, permissions: string[]) => void
  logout: () => void
  hasPermission: (code: string) => boolean
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
    }),
    { name: 'mms-auth' }
  )
)