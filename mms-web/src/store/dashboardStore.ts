import { create } from 'zustand'
import type { DashboardSnapshot } from '../types/dashboard'

interface DashboardStore {
  snapshot: DashboardSnapshot | null
  isLoading: boolean
  lastUpdated: Date | null
  setSnapshot: (snapshot: DashboardSnapshot) => void
  setLoading: (loading: boolean) => void
}

export const useDashboardStore = create<DashboardStore>((set) => ({
  snapshot: null,
  isLoading: false,
  lastUpdated: null,
  setSnapshot: (snapshot) => set({ snapshot, lastUpdated: new Date() }),
  setLoading: (isLoading) => set({ isLoading }),
}))