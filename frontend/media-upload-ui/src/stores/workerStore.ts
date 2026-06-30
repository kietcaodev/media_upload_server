import { create } from 'zustand';
import { workerApi } from '../api/services';
import type { WorkerStatusDto } from '../types';

interface WorkerStore {
  status: WorkerStatusDto | null;
  loading: boolean;
  fetch: () => Promise<void>;
  pause: (reason?: string) => Promise<void>;
  resume: () => Promise<void>;
}

export const useWorkerStore = create<WorkerStore>((set) => ({
  status: null,
  loading: false,

  fetch: async () => {
    set({ loading: true });
    try {
      const status = await workerApi.status();
      set({ status });
    } finally {
      set({ loading: false });
    }
  },

  pause: async (reason?: string) => {
    const status = await workerApi.pause(reason);
    set({ status });
  },

  resume: async () => {
    const status = await workerApi.resume();
    set({ status });
  },
}));
