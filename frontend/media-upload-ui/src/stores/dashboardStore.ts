import { create } from 'zustand';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { dashboardApi } from '../api/services';
import { useJobStore } from './jobStore';
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
    if (get().connection) return;
    const apiUrl = import.meta.env.VITE_API_URL || 'http://localhost:5000';

    // KHÔNG truyền accessTokenFactory: với WebSocket, SignalR gắn token vào URL
    // (?access_token=...) → base64(user:pass) của admin lọt vào access log nginx.
    // Hub không yêu cầu auth nên không cần token.
    const conn = new HubConnectionBuilder()
      .withUrl(`${apiUrl}/hubs/jobs`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    // Gộp các event dồn dập (worker xử lý nhiều job song song) thành 1 lần refresh.
    let timer: ReturnType<typeof setTimeout> | undefined;
    const refresh = () => {
      clearTimeout(timer);
      timer = setTimeout(() => {
        get().fetchStats();
        const jobs = useJobStore.getState();
        jobs.fetch(jobs.page);
      }, 1000);
    };

    conn.on('job:created', refresh);
    conn.on('job:statusChanged', refresh);
    conn.on('worker:status', (status: WorkerStatusDto) => {
      set(state => ({
        stats: state.stats
          ? { ...state.stats, workerPaused: status.isPaused, workerPauseReason: status.pauseReason, activeWorkers: status.activeCount }
          : state.stats
      }));
    });

    conn.start().catch(console.error);
    set({ connection: conn });
  },

  stopSignalR: () => {
    get().connection?.stop();
    set({ connection: null });
  },
}));
