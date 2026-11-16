import { useState } from 'react';
import type { ReactNode } from 'react';
import { AnimatePresence, motion } from 'framer-motion';
import { AudioLines, Download, Info, Monitor, Settings2, X } from 'lucide-react';
import type { Settings } from '../hooks/useSettings';
import { GAME } from '../styles/theme';

type Category = 'general' | 'display' | 'download' | 'audio' | 'about';

const CATEGORIES: { key: Category; label: string; icon: typeof Settings2 }[] = [
  { key: 'general', label: 'GENERAL · 通用', icon: Settings2 },
  { key: 'display', label: 'DISPLAY · 显示', icon: Monitor },
  { key: 'download', label: 'DOWNLOAD · 下载', icon: Download },
  { key: 'audio', label: 'AUDIO · 音频', icon: AudioLines },
  { key: 'about', label: 'ABOUT · 关于', icon: Info },
];

interface SettingsModalProps {
  open: boolean;
  onClose: () => void;
  settings: Settings;
  onChange: (patch: Partial<Settings>) => void;
}

function Row({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-4 border-b border-moon-white/8 py-3.5 last:border-0">
      <div>
        <p className="text-[14px] text-moon-white">{label}</p>
        {hint && <p className="mt-0.5 text-[12px] text-silver/60">{hint}</p>}
      </div>
      <div className="shrink-0">{children}</div>
    </div>
  );
}

function Toggle({ value, onChange, label }: { value: boolean; onChange: (v: boolean) => void; label: string }) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={value}
      aria-label={label}
      onClick={() => onChange(!value)}
      className={`relative h-6 w-11 rounded-full transition-colors ${value ? 'bg-ultra-starry' : 'bg-silver/25'}`}
    >
      <span
        className={`absolute top-0.5 h-5 w-5 rounded-full bg-moon-white shadow transition-all ${value ? 'left-[22px]' : 'left-0.5'}`}
      />
    </button>
  );
}

function Select({ value, options, onChange, label }: { value: string; options: string[]; onChange: (v: string) => void; label: string }) {
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      aria-label={label}
      className="cursor-pointer rounded-lg border border-moon-white/20 bg-night/60 px-3 py-2 text-[13px] text-moon-white outline-none focus:border-ultra-moon [&>option]:bg-ultra-deep"
    >
      {options.map((o) => (
        <option key={o} value={o}>
          {o}
        </option>
      ))}
    </select>
  );
}

/**
 * Settings modal — ultramarine glass popup, category nav on the left,
 * details on the right. All changes persist to localStorage.
 */
export default function SettingsModal({ open, onClose, settings, onChange }: SettingsModalProps) {
  const [category, setCategory] = useState<Category>('general');

  return (
    <AnimatePresence>
      {open && (
        <motion.div
          className="fixed inset-0 z-50 flex items-center justify-center p-4"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
        >
          <div className="absolute inset-0 bg-abyss/70 backdrop-blur-sm" onClick={onClose} aria-hidden="true" />
          <motion.div
            role="dialog"
            aria-modal="true"
            aria-label="启动器设置"
            initial={{ opacity: 0, y: 24, scale: 0.97 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 16, scale: 0.97 }}
            transition={{ duration: 0.28, ease: 'easeOut' }}
            className="glass-panel relative flex h-[min(560px,92vh)] w-full max-w-3xl overflow-hidden rounded-2xl"
          >
            {/* Category nav */}
            <aside className="flex w-52 shrink-0 flex-col gap-1 border-r border-moon-white/10 bg-night/40 p-4">
              <p className="px-2 pb-3 font-display text-[15px] font-semibold tracking-[0.3em] text-moon-white">
                SETTINGS
              </p>
              {CATEGORIES.map((c) => (
                <button
                  key={c.key}
                  type="button"
                  onClick={() => setCategory(c.key)}
                  className={`flex items-center gap-2.5 rounded-lg px-3 py-2.5 text-left text-[13px] tracking-wide transition-colors ${
                    category === c.key
                      ? 'bg-ultra-classic/45 text-moon-white shadow-[inset_0_0_0_1px_rgba(120,151,219,0.45)]'
                      : 'text-silver/75 hover:bg-ultra-deep/50 hover:text-moon-white'
                  }`}
                >
                  <c.icon className="h-4 w-4 shrink-0" aria-hidden="true" />
                  {c.label}
                </button>
              ))}
              <p className="mt-auto px-2 pt-4 text-[11px] leading-relaxed text-silver/50">
                ASTRAL BLOOM LAUNCHER
                <br />
                v{GAME.launcherVersion}
              </p>
            </aside>

            {/* Content */}
            <div className="min-w-0 flex-1 overflow-y-auto p-6">
              <div className="mb-2 flex items-center justify-between">
                <h3 className="font-display text-[20px] font-semibold tracking-[0.14em] text-moon-white">
                  {CATEGORIES.find((c) => c.key === category)?.label}
                </h3>
                <button
                  type="button"
                  onClick={onClose}
                  className="rounded-full p-1.5 text-silver transition-colors hover:bg-ultra-deep/60 hover:text-moon-white"
                  aria-label="关闭设置"
                >
                  <X className="h-5 w-5" />
                </button>
              </div>

              {category === 'general' && (
                <div>
                  <Row label="启动器语言" hint="Launcher language">
                    <Select
                      label="启动器语言"
                      value={settings.language === 'zh' ? '简体中文' : settings.language === 'en' ? 'English' : '日本語'}
                      options={['简体中文', 'English', '日本語']}
                      onChange={(v) =>
                        onChange({ language: v === '简体中文' ? 'zh' : v === 'English' ? 'en' : 'ja' })
                      }
                    />
                  </Row>
                  <Row label="开机启动" hint="启动系统时自动运行启动器">
                    <Toggle label="开机启动" value={settings.launchAtStartup} onChange={(v) => onChange({ launchAtStartup: v })} />
                  </Row>
                  <Row label="自动更新" hint="有新版本时自动下载更新">
                    <Toggle label="自动更新" value={settings.autoUpdate} onChange={(v) => onChange({ autoUpdate: v })} />
                  </Row>
                  <Row label="关闭时最小化" hint="关闭窗口时最小化到托盘">
                    <Toggle label="关闭时最小化" value={settings.minimizeOnClose} onChange={(v) => onChange({ minimizeOnClose: v })} />
                  </Row>
                  <Row label="自动登录" hint="下次打开时使用记住的账号自动登录">
                    <Toggle label="自动登录" value={settings.autoLogin} onChange={(v) => onChange({ autoLogin: v })} />
                  </Row>
                </div>
              )}

              {category === 'display' && (
                <div>
                  <Row label="窗口模式" hint="全屏 / 窗口 / 无边框">
                    <Select
                      label="窗口模式"
                      value={settings.windowMode === 'fullscreen' ? '全屏' : settings.windowMode === 'windowed' ? '窗口' : '无边框窗口'}
                      options={['全屏', '窗口', '无边框窗口']}
                      onChange={(v) =>
                        onChange({
                          windowMode: v === '全屏' ? 'fullscreen' : v === '窗口' ? 'windowed' : 'borderless',
                        })
                      }
                    />
                  </Row>
                  <Row label="分辨率" hint="游戏运行分辨率">
                    <Select
                      label="分辨率"
                      value={settings.resolution}
                      options={['2560 × 1440', '1920 × 1080', '1600 × 900', '1366 × 768']}
                      onChange={(v) => onChange({ resolution: v })}
                    />
                  </Row>
                  <Row label="UI 缩放" hint={`${settings.uiScale}%`}>
                    <input
                      type="range"
                      min={80}
                      max={140}
                      step={5}
                      value={settings.uiScale}
                      onChange={(e) => onChange({ uiScale: Number(e.target.value) })}
                      className="w-44 accent-[#4169C9]"
                      aria-label="UI 缩放"
                    />
                  </Row>
                </div>
              )}

              {category === 'download' && (
                <div>
                  <Row label="下载路径" hint="游戏文件安装位置">
                    <span className="max-w-56 truncate rounded-lg border border-moon-white/20 bg-night/60 px-3 py-2 text-[12px] text-silver">
                      {settings.downloadPath}
                    </span>
                  </Row>
                  <Row label="下载速度限制" hint={settings.speedLimitMbps === 0 ? '不限速' : `${settings.speedLimitMbps} MB/s`}>
                    <Select
                      label="下载速度限制"
                      value={settings.speedLimitMbps === 0 ? '不限速' : `${settings.speedLimitMbps} MB/s`}
                      options={['不限速', '5 MB/s', '10 MB/s', '20 MB/s', '50 MB/s']}
                      onChange={(v) =>
                        onChange({ speedLimitMbps: v === '不限速' ? 0 : Number(v.replace(' MB/s', '')) })
                      }
                    />
                  </Row>
                  <Row label="缓存清理" hint="清理启动器临时缓存文件">
                    <button
                      type="button"
                      onClick={() => window.alert('演示模式：缓存已清理（模拟）。')}
                      className="rounded-lg border border-ultra-moon/50 bg-ultra-classic/30 px-3.5 py-2 text-[13px] text-moon-white transition-colors hover:bg-ultra-classic/50"
                    >
                      清理缓存
                    </button>
                  </Row>
                </div>
              )}

              {category === 'audio' && (
                <div>
                  <Row label="背景音乐" hint={settings.musicEnabled ? '已开启 · 未配置音频文件，保持静音' : '已关闭'}>
                    <Toggle label="背景音乐" value={settings.musicEnabled} onChange={(v) => onChange({ musicEnabled: v })} />
                  </Row>
                  <Row label="背景音乐音量" hint={`${settings.bgmVolume}% · 无音频文件时静音`}>
                    <input
                      type="range"
                      min={0}
                      max={100}
                      value={settings.bgmVolume}
                      onChange={(e) => onChange({ bgmVolume: Number(e.target.value) })}
                      className="w-44 accent-[#4169C9]"
                      aria-label="背景音乐音量"
                    />
                  </Row>
                  <Row label="音效音量" hint={`${settings.sfxVolume}%`}>
                    <input
                      type="range"
                      min={0}
                      max={100}
                      value={settings.sfxVolume}
                      onChange={(e) => onChange({ sfxVolume: Number(e.target.value) })}
                      className="w-44 accent-[#4169C9]"
                      aria-label="音效音量"
                    />
                  </Row>
                  <p className="mt-4 rounded-lg border border-pale-gold/25 bg-pale-gold/8 px-3.5 py-3 text-[12.5px] leading-relaxed text-pale-gold/90">
                    本演示未附带任何版权音乐，默认保持静音。正式版可在 assets/audio 中配置原创 BGM 后启用。
                  </p>
                </div>
              )}

              {category === 'about' && (
                <div className="flex flex-col gap-3 text-[14px] leading-relaxed text-silver">
                  <p>
                    <span className="text-moon-white">启动器版本：</span>v{GAME.launcherVersion}
                  </p>
                  <p>
                    <span className="text-moon-white">游戏版本：</span>v{GAME.gameVersion} · 星樱物语
                  </p>
                  <p>
                    ASTRAL BLOOM — 星樱物语是一款原创二次元幻想游戏。本启动器内的角色、Logo、背景与世界观均为原创设计。
                  </p>
                  <div className="flex gap-3 pt-1">
                    {['用户协议', '隐私政策', '官方网站'].map((label) => (
                      <button
                        key={label}
                        type="button"
                        onClick={() => window.alert(`演示模式：「${label}」页面尚未上线。`)}
                        className="rounded-lg border border-moon-white/20 bg-ultra-deep/40 px-3.5 py-2 text-[13px] text-moon-white transition-colors hover:border-ultra-moon/60"
                      >
                        {label}
                      </button>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
