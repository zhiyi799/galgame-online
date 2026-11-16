import { useCallback, useEffect, useState } from 'react';

export type Language = 'zh' | 'en' | 'ja';
export type WindowMode = 'fullscreen' | 'windowed' | 'borderless';

export interface Settings {
  language: Language;
  launchAtStartup: boolean;
  autoUpdate: boolean;
  minimizeOnClose: boolean;
  autoLogin: boolean;
  windowMode: WindowMode;
  resolution: string;
  uiScale: number;
  downloadPath: string;
  speedLimitMbps: number; // 0 = unlimited
  musicEnabled: boolean;
  bgmVolume: number; // 0–100
  sfxVolume: number; // 0–100
}

export const defaultSettings: Settings = {
  language: 'zh',
  launchAtStartup: false,
  autoUpdate: true,
  minimizeOnClose: true,
  autoLogin: false,
  windowMode: 'windowed',
  resolution: '1920 × 1080',
  uiScale: 100,
  downloadPath: 'C:\\Games\\ASTRAL BLOOM',
  speedLimitMbps: 0,
  musicEnabled: false,
  bgmVolume: 60,
  sfxVolume: 70,
};

const STORAGE_KEY = 'astral-bloom-settings';

/**
 * Launcher settings, persisted to localStorage.
 * NOTE: no audio file is shipped with this demo, so enabling music only
 * changes the toggle state — the launcher stays silent and the UI says so.
 */
export function useSettings() {
  const [settings, setSettings] = useState<Settings>(() => {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? { ...defaultSettings, ...(JSON.parse(raw) as Partial<Settings>) } : defaultSettings;
    } catch {
      return defaultSettings;
    }
  });

  useEffect(() => {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(settings));
    } catch {
      /* storage unavailable */
    }
  }, [settings]);

  const update = useCallback((patch: Partial<Settings>) => {
    setSettings((prev) => ({ ...prev, ...patch }));
  }, []);

  return { settings, update };
}
