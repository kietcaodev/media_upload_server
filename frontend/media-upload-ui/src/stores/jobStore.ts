import { create } from 'zustand';
import { jobsApi } from '../api/services';
import type { JobDto, JobListResponse } from '../types';

interface JobStore {
  jobs: JobDto[];
  total: number;
  page: number;
  pageSize: number;
  statusFilter: string;
  loading: boolean;
  fetch: (page?: number, status?: string) => Promise<void>;
  cancel: (id: string) => Promise<void>;
  retry: (id: string) => Promise<void>;
  setFilter: (status: string) => void;
}

export const useJobStore = create<JobStore>((set, get) => ({
  jobs: [],
  total: 0,
  page: 1,
  pageSize: 20,
  statusFilter: '',
  loading: false,

  fetch: async (page = 1, status?: string) => {
    set({ loading: true });
    try {
      const s = status !== undefined ? status : get().statusFilter;
      const data: JobListResponse = await jobsApi.list(page, get().pageSize, s || undefined);
      set({ jobs: data.items, total: data.total, page, statusFilter: s });
    } finally {
      set({ loading: false });
    }
  },

  cancel: async (id: string) => {
    await jobsApi.cancel(id);
    get().fetch(get().page);
  },

  retry: async (id: string) => {
    await jobsApi.retry(id);
    get().fetch(get().page);
  },

  setFilter: (status: string) => {
    set({ statusFilter: status });
    get().fetch(1, status);
  },
}));
