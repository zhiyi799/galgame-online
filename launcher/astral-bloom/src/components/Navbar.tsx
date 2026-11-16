import { useState } from 'react';
import { Globe, LogOut, Settings, User, Volume2, VolumeX } from 'lucide-react';
import type { DemoUser } from '../hooks/useAuth';
import type { Language } from '../hooks/useSettings';

const NAV_ITEMS = [
  { key: 'home', en: 'HOME', zh: '首页' },
  { key: 'news', en: 'NEWS', zh: '资讯' },
  { key: 'community', en: 'COMMUNITY', zh: '社区' },
  { key: 'support', en: 'SUPPORT', zh: '支持' },
] as const;

type NavKey = (typeof NAV_ITEMS)[number]['key'];

interface NavbarProps {
  user: DemoUser | null;
  musicEnabled: boolean;
  language: Language;
  onLanguageChange: (lang: Language) => void;
  onToggleMusic: () => void;
  onOpenSettings: () => void;
  onLogout?: () => void;
  onGoHome?: () => void;
}

/** Small original star-sakura mark. */
export function LogoMark({ size = 34 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 40 40" aria-hidden="true">
      <circle cx="20" cy="20" r="18.5" fill="none" stroke="#7897DB" strokeWidth="1.2" opacity="0.85" />
      <circle cx="20" cy="20" r="14.5" fill="none" stroke="#C9D4E8" strokeWidth="0.7" opacity="0.5" />
      <path
        d="M20 7 L22.6 16.4 L32 16.4 L24.9 22.2 L27.4 31.5 L20 26 L12.6 31.5 L15.1 22.2 L8 16.4 L17.4 16.4 Z"
        fill="#4169C9"
      />
      <circle cx="20" cy="20" r="2.4" fill="#E5EAF7" />
      <circle cx="29" cy="10" r="1.3" fill="#DDAFCB" />
      <circle cx="11" cy="29" r="1" fill="#D9CBAA" />
    </svg>
  );
}

/**
 * Lightweight transparent top bar: logo left, nav centre, controls right.
 * Hover shows a pale-blue glow and a hairline underline.
 */
export default function Navbar({
  user,
  musicEnabled,
  language,
  onLanguageChange,
  onToggleMusic,
  onOpenSettings,
  onLogout,
  onGoHome,
}: NavbarProps) {
  const [active, setActive] = useState<NavKey>('home');

  return (
    <header className="relative z-30 flex h-16 shrink-0 items-center justify-between gap-4 border-b border-moon-white/10 bg-ultra-deep/25 px-4 backdrop-blur-md sm:px-6 lg:px-10">
      <button
        type="button"
        onClick={onGoHome}
        className="group flex items-center gap-3"
        aria-label="ASTRAL BLOOM 首页"
      >
        <LogoMark />
        <span className="text-left leading-none">
          <span className="block font-display text-[19px] font-semibold tracking-[0.22em] text-moon-white transition-colors group-hover:text-white">
            ASTRAL BLOOM
          </span>
          <span className="mt-1 block text-[11px] tracking-[0.5em] text-silver/80">
            星樱物语
          </span>
        </span>
      </button>

      <nav className="hidden items-center gap-8 md:flex" aria-label="主导航">
        {NAV_ITEMS.map((item) => (
          <button
            key={item.key}
            type="button"
            onClick={() => setActive(item.key)}
            className={`group relative pb-1 text-[13px] font-medium tracking-[0.3em] transition-colors ${
              active === item.key ? 'text-moon-white' : 'text-silver/70 hover:text-moon-white'
            }`}
          >
            {item.en}
            <span
              className={`absolute inset-x-0 -bottom-px h-px bg-ultra-moon transition-all duration-300 ${
                active === item.key ? 'opacity-100 shadow-[0_0_8px_rgba(120,151,219,0.9)]' : 'opacity-0 group-hover:opacity-60'
              }`}
            />
          </button>
        ))}
      </nav>

      <div className="flex items-center gap-1 sm:gap-2">
        <label className="flex items-center gap-1.5 px-2 py-1.5 text-silver">
          <Globe className="h-4 w-4" aria-hidden="true" />
          <select
            value={language}
            onChange={(e) => onLanguageChange(e.target.value as Language)}
            className="cursor-pointer bg-transparent text-[12px] font-medium text-moon-white outline-none [&>option]:bg-ultra-deep"
            aria-label="语言切换"
          >
            <option value="zh">中文</option>
            <option value="en">EN</option>
            <option value="ja">日本語</option>
          </select>
        </label>

        <button
          type="button"
          onClick={onToggleMusic}
          className="p-2 text-silver transition-colors hover:text-moon-white"
          title={musicEnabled ? '音乐：已开启（未配置音频文件，保持静音）' : '音乐：已关闭'}
          aria-label="背景音乐开关"
          aria-pressed={musicEnabled}
        >
          {musicEnabled ? <Volume2 className="h-4 w-4" /> : <VolumeX className="h-4 w-4" />}
        </button>

        {user ? (
          <div className="flex items-center gap-2 py-1 pl-1 pr-2">
            <span className="flex h-7 w-7 items-center justify-center rounded-full bg-ultra-classic text-[13px] font-semibold text-moon-white">
              {user.nickname.slice(0, 1).toUpperCase()}
            </span>
            <span className="max-w-24 truncate text-[13px] font-medium text-moon-white">
              {user.nickname}
            </span>
            <button
              type="button"
              onClick={onLogout}
              className="text-silver/70 transition-colors hover:text-sakura"
              title="退出登录"
              aria-label="退出登录"
            >
              <LogOut className="h-4 w-4" />
            </button>
          </div>
        ) : (
          <button
            type="button"
            className="p-2 text-silver transition-colors hover:text-moon-white"
            title="用户中心（登录后可用）"
            aria-label="用户中心"
          >
            <User className="h-4 w-4" />
          </button>
        )}

        <button
          type="button"
          onClick={onOpenSettings}
          className="p-2 text-silver transition-colors hover:text-moon-white"
          title="设置"
          aria-label="设置"
        >
          <Settings className="h-4 w-4" />
        </button>
      </div>
    </header>
  );
}
