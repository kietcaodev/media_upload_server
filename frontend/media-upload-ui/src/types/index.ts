// ── Shared types matching backend DTOs ──────────────────────────────

export type JobStatus = 'Pending' | 'Processing' | 'Success' | 'Failed' | 'Cancelled' | 'Paused';
export type AuthType = 'Bearer' | 'Basic' | 'ApiKey';

export interface JobDto {
  id: string;
  fileId: string;
  originalFileName: string;
  fileSize: number;
  erpTarget: string;
  status: JobStatus;
  retryCount: number;
  maxRetry: number;
  lastError?: string;
  createdAtUtc: string;
  processedAtUtc?: string;
  completedAtUtc?: string;
}

export interface JobListResponse {
  items: JobDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface DashboardStats {
  totalJobs: number;
  pendingJobs: number;
  processingJobs: number;
  successJobs: number;
  failedJobs: number;
  cancelledJobs: number;
  workerPaused: boolean;
  workerPauseReason?: string;
  activeWorkers: number;
  withinTimeWindow: boolean;
}

export interface TimelineItem {
  date: string;
  success: number;
  failed: number;
  pending: number;
}

export interface TimeWindowDto {
  id: number;
  name: string;
  startTime: string;
  endTime: string;
  daysOfWeek: string;
  enabled: boolean;
}

export interface ErpEndpointDto {
  id: number;
  target: string;
  url: string;
  tokenPrefix: string;
  enabled: boolean;
}

export interface CredentialDto {
  id: number;
  name: string;
  authType: AuthType;
  tokenPrefix: string;
  username?: string;
  canUpload: boolean;
  canReadJobs: boolean;
  canConfig: boolean;
  allowedErp: string;
  enabled: boolean;
  createdAtUtc: string;
  lastUsedAtUtc?: string;
}

export interface SettingDto {
  key: string;
  value: string;
  description?: string;
  hotReload: boolean;
  updatedAtUtc: string;
}

export interface WorkerStatusDto {
  isPaused: boolean;
  pauseReason?: string;
  activeCount: number;
}

export interface CreateCredentialResponse {
  id: number;
  name: string;
  authType: string;
  rawToken: string;
  username?: string;
  tokenPrefix: string;
}
