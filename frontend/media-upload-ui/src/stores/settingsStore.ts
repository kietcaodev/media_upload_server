import { create } from 'zustand';
import { settingsApi } from '../api/services';
import { setAppTimezone } from '../api/client';
import type { SettingDto } from '../types';

interface SettingsStore {
  settings: SettingDto[];
  loading: boolean;
  saving: boolean;
  fetch: () => Promise<void>;
  save: (updates: Record<string, string>) => Promise<void>;
  reset: (key: string) => Promise<void>;
}

export const useSettingsStore = create<SettingsStore>((set, get) => ({
  settings: [],
  loading: false,
  saving: false,

  fetch: async () => {
    set({ loading: true });
    try {
      const list = await settingsApi.list();
      set({ settings: list });
      // Update APP_TZ from fetched settings
      const tzSetting = list.find(s => s.key === 'system.timezone');
      if (tzSetting) setAppTimezone(tzSetting.value);
    } finally {
      set({ loading: false });
    }
  },

  save: async (updates: Record<string, string>) => {
    set({ saving: true });
    try {
      await settingsApi.patch(updates);
      // Refresh
      await get().fetch();
    } finally {
      set({ saving: false });
    }
  },

  reset: async (key: string) => {
    await settingsApi.reset(key);
    await get().fetch();
  },
}));
