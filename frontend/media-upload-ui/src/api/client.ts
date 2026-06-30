import axios from 'axios';
import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc';
import timezone from 'dayjs/plugin/timezone';

dayjs.extend(utc);
dayjs.extend(timezone);

export let APP_TZ = 'Asia/Ho_Chi_Minh';

export function setAppTimezone(tz: string) {
  APP_TZ = tz;
}

/** Format a UTC ISO string to local timezone for display */
export function formatLocalTime(utcStr: string | undefined | null, fmt = 'DD/MM/YYYY HH:mm:ss'): string {
  if (!utcStr) return '-';
  return dayjs(utcStr).tz(APP_TZ).format(fmt);
}

/** Convert a local dayjs object to UTC ISO string for API */
export function toUtcIso(local: dayjs.Dayjs): string {
  return local.tz(APP_TZ, true).utc().toISOString();
}

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5000',
  timeout: 30000,
});

// Inject auth token from localStorage
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('auth_token');
  const authType = localStorage.getItem('auth_type') || 'Bearer';
  if (token) {
    if (authType === 'ApiKey') {
      config.headers['X-Api-Key'] = token;
    } else {
      config.headers['Authorization'] = `${authType} ${token}`;
    }
  }
  return config;
});

export default api;
