import { create } from 'zustand';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { dashboardApi } from '../api/services';
import type { DashboardStats, TimelineItem, JobDto, WorkerStatusDto } from '../types';

interface DashboardStore {
  stats: DashboardStats | null;
  timeline: TimelineItem[];
  recentJobs: JobDto[];
  loading: boolean;
  connection: HubConnection | null;
  fetchStats: () => Promise<void>;
  fetchTimeline: (days?: number) => Promise<void>;
  startSignalR: () => void;
  stopSignalR: () => void;
}

export const useDashboardStore = create<DashboardStore>((set, get) => ({
  stats: null,
  timeline: [],
  recentJobs: [],
  loading: false,
  connection: null,

  fetchStats: async () => {
    set({ loading: true });
    try {
      const stats = await dashboardApi.stats();
      set({ stats });
    } finally {
      set({ loading: false });
    }
  },

  fetchTimeline: async (days = 7) => {
    const timeline = await dashboardApi.timeline(days);
    set({ timeline });
  },

  startSignalR: () => {
    const apiUrl = import.meta.env.VITE_API_URL || 'http://localhost:5000';
    const token = localStorage.getItem('auth_token') || '';

    const conn = new HubConnectionBuilder()
      .withUrl(`${apiUrl}/hubs/jobs`, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    conn.on('job:created', () => get().fetchStats());
    conn.on('job:statusChanged', () => get().fetchStats());
    conn.on('worker:status', (status: WorkerStatusDto) => {
      set(state => ({
        stats: state.stats
          ? { ...state.stats, workerPaused: status.isPaused, workerPauseReason: status.pauseReason, activeWorkers: status.activeCount }
          : state.stats
      }));
    });
    conn.on('stats:updated', (stats: DashboardStats) => set({ stats }));

    conn.start().catch(console.error);
    set({ connection: conn });
  },

  stopSignalR: () => {
    get().connection?.stop();
    set({ connection: null });
  },
}));
