import api from './client';
import type {
  JobListResponse, DashboardStats, TimelineItem,
  TimeWindowDto, ErpEndpointDto, CredentialDto,
  SettingDto, WorkerStatusDto, CreateCredentialResponse
} from '../types';

// ── Dashboard ────────────────────────────────────────────────────────
export const dashboardApi = {
  stats: () => api.get<DashboardStats>('/api/dashboard/stats').then(r => r.data),
  timeline: (days = 7) => api.get<TimelineItem[]>(`/api/dashboard/timeline?days=${days}`).then(r => r.data),
};

// ── Jobs ─────────────────────────────────────────────────────────────
export const jobsApi = {
  list: (page = 1, pageSize = 20, status?: string) => {
    const q = status ? `&status=${status}` : '';
    return api.get<JobListResponse>(`/api/jobs?page=${page}&pageSize=${pageSize}${q}`).then(r => r.data);
  },
  cancel: (id: string) => api.patch(`/api/jobs/${id}/cancel`).then(r => r.data),
  retry: (id: string) => api.patch(`/api/jobs/${id}/retry`).then(r => r.data),
};

// ── Upload ───────────────────────────────────────────────────────────
export const uploadApi = {
  upload: (files: File[], meta: Record<string, string>) => {
    const form = new FormData();
    files.forEach(f => form.append('files', f));
    Object.entries(meta).forEach(([k, v]) => form.append(k, v));
    return api.post('/api/upload', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }).then(r => r.data);
  },
};

// ── Worker ───────────────────────────────────────────────────────────
export const workerApi = {
  status: () => api.get<WorkerStatusDto>('/api/worker/status').then(r => r.data),
  pause: (reason?: string) => api.post('/api/worker/pause', { reason }).then(r => r.data),
  resume: () => api.post('/api/worker/resume').then(r => r.data),
};

// ── Time Windows ─────────────────────────────────────────────────────
export const timeWindowApi = {
  list: () => api.get<TimeWindowDto[]>('/api/config/timewindows').then(r => r.data),
  create: (d: Omit<TimeWindowDto, 'id'>) => api.post<TimeWindowDto>('/api/config/timewindows', d).then(r => r.data),
  update: (id: number, d: Omit<TimeWindowDto, 'id'>) => api.put<TimeWindowDto>(`/api/config/timewindows/${id}`, d).then(r => r.data),
  remove: (id: number) => api.delete(`/api/config/timewindows/${id}`),
};

// ── ERP ──────────────────────────────────────────────────────────────
export const erpApi = {
  list: () => api.get<ErpEndpointDto[]>('/api/config/erp').then(r => r.data),
  upsert: (d: { target: string; url: string; token: string; enabled: boolean }) =>
    api.put<ErpEndpointDto>('/api/config/erp', d).then(r => r.data),
};

// ── Credentials ──────────────────────────────────────────────────────
export const credentialApi = {
  list: () => api.get<CredentialDto[]>('/api/credentials').then(r => r.data),
  create: (d: object) => api.post<CreateCredentialResponse>('/api/credentials', d).then(r => r.data),
  update: (id: number, d: object) => api.put(`/api/credentials/${id}`, d).then(r => r.data),
  remove: (id: number) => api.delete(`/api/credentials/${id}`),
  rotate: (id: number) => api.post<{ rawToken: string; tokenPrefix: string }>(`/api/credentials/${id}/rotate`).then(r => r.data),
};

// ── Settings ─────────────────────────────────────────────────────────
export const settingsApi = {
  list: () => api.get<SettingDto[]>('/api/config/settings').then(r => r.data),
  patch: (updates: Record<string, string>) => api.patch('/api/config/settings', { updates }).then(r => r.data),
  reset: (key: string) => api.post(`/api/config/settings/reset/${key}`).then(r => r.data),
};
