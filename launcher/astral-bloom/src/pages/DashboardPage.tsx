import { useState } from 'react';
import { motion } from 'framer-motion';
import { CalendarDays, CheckCircle2, Megaphone, RefreshCw, Sparkles } from 'lucide-react';
import BackgroundScene from '../components/BackgroundScene';
import CharacterDisplay from '../components/CharacterDisplay';
import DownloadPanel from '../components/DownloadPanel';
import Navbar from '../components/Navbar';
import StatusBar from '../components/StatusBar';
import { useParallax } from '../hooks/useParallax';
import type { DemoUser } from '../hooks/useAuth';
import type { Language } from '../hooks/useSettings';
import { GAME } from '../styles/theme';
import bgImage from '../assets/backgrounds/town-night.webp';

interface DashboardPageProps {
  user: DemoUser;
  musicEnabled: boolean;
  language: Language;
  onLanguageChange: (lang: Language) => void;
  onToggleMusic: () => void;
  onOpenSettings: () => void;
  onLogout: () => void;
}

const UPDATE_NOTES = [
  { version: 'v1.0.0', text: '星樱物语正式上线，群青幻想世界开启。' },
  { version: 'v1.0.0', text: '新增限时活动「星樱祭 · 樱落之时」。' },
  { version: 'v1.0.0', text: '优化群青夜空渲染与樱花粒子表现。' },
  { version: 'v1.0.0', text: '修复若干已知问题，提升启动稳定性。' },
];

const EVENTS = [
  { title: '星樱祭 · 樱落之时', date: '11月16日 — 11月30日', tag: '限时活动' },
  { title: '月海巡礼 · 新章节', date: '12月1日开启', tag: '新章节' },
];

/**
 * Post-login launcher home — a real launcher page, not an admin template.
 * Left: version / update notes / events. Centre: promo illustration,
 * character and world intro. Right: download / launch panel.
 */
export default function DashboardPage({
  user,
  musicEnabled,
  language,
  onLanguageChange,
  onToggleMusic,
  onOpenSettings,
  onLogout,
}: DashboardPageProps) {
  const parallax = useParallax(10);
  const [updateState, setUpdateState] = useState<'idle' | 'checking' | 'latest'>('idle');

  const checkUpdate = () => {
    setUpdateState('checking');
    window.setTimeout(() => setUpdateState('latest'), 1100);
  };

  return (
    <div className="relative flex h-dvh flex-col overflow-hidden">
      <BackgroundScene parallax={parallax} dim />
      <Navbar
        user={user}
        musicEnabled={musicEnabled}
        language={language}
        onLanguageChange={onLanguageChange}
        onToggleMusic={onToggleMusic}
        onOpenSettings={onOpenSettings}
        onLogout={onLogout}
      />

      <main className="relative z-20 min-h-0 flex-1 overflow-y-auto">
        <div className="mx-auto grid w-full max-w-[1920px] grid-cols-1 gap-5 p-4 sm:p-6 lg:grid-cols-[290px_minmax(0,1fr)_380px] lg:px-8">
          {/* Left — version / updates / events */}
          <motion.aside
            initial={{ opacity: 0, x: -18 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ duration: 0.55, ease: 'easeOut' }}
            className="flex flex-col gap-5"
          >
            <section className="glass-panel rounded-2xl p-5">
              <h3 className="flex items-center gap-2 text-[12px] font-semibold tracking-[0.3em] text-ultra-moon">
                <Megaphone className="h-4 w-4" aria-hidden="true" />
                游戏版本
              </h3>
              <p className="mt-3 font-display text-[30px] font-semibold text-moon-white">
                v{GAME.gameVersion}
              </p>
              <p className="mt-1 text-[13px] text-silver/75">
                {updateState === 'latest'
                  ? '已是最新版本（模拟检查）。'
                  : '正式版 · 星樱物语'}
              </p>
              <button
                type="button"
                onClick={checkUpdate}
                disabled={updateState === 'checking'}
                className="mt-4 flex items-center gap-2 rounded-lg border border-ultra-moon/50 bg-ultra-classic/30 px-3.5 py-2 text-[13px] text-moon-white transition-colors hover:bg-ultra-classic/50 disabled:opacity-60"
              >
                <RefreshCw className={`h-4 w-4 ${updateState === 'checking' ? 'animate-spin' : ''}`} aria-hidden="true" />
                {updateState === 'checking' ? '检查中…' : '检查更新'}
              </button>
            </section>

            <section className="glass-panel rounded-2xl p-5">
              <h3 className="text-[12px] font-semibold tracking-[0.3em] text-ultra-moon">
                更新公告
              </h3>
              <ul className="mt-3 flex flex-col gap-3">
                {UPDATE_NOTES.map((n, i) => (
                  <li key={i} className="flex gap-2.5 text-[13px] leading-relaxed text-silver">
                    <span className="mt-1.5 h-1.5 w-1.5 shrink-0 rotate-45 bg-sakura/80" aria-hidden="true" />
                    <span>
                      <span className="mr-1.5 text-[11px] text-pale-gold">{n.version}</span>
                      {n.text}
                    </span>
                  </li>
                ))}
              </ul>
            </section>

            <section className="glass-panel rounded-2xl p-5">
              <h3 className="flex items-center gap-2 text-[12px] font-semibold tracking-[0.3em] text-ultra-moon">
                <CalendarDays className="h-4 w-4" aria-hidden="true" />
                最新活动
              </h3>
              <ul className="mt-3 flex flex-col gap-3">
                {EVENTS.map((e) => (
                  <li key={e.title} className="rounded-xl bg-night/45 px-3.5 py-3 ring-1 ring-moon-white/10">
                    <p className="text-[14px] font-medium text-moon-white">{e.title}</p>
                    <p className="mt-1 flex items-center justify-between text-[12px] text-silver/70">
                      {e.date}
                      <span className="rounded-full bg-ultra-starry/25 px-2 py-0.5 text-[11px] text-ultra-moon">
                        {e.tag}
                      </span>
                    </p>
                  </li>
                ))}
              </ul>
            </section>
          </motion.aside>

          {/* Centre — promo + world intro */}
          <motion.div
            initial={{ opacity: 0, y: 18 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.55, delay: 0.08, ease: 'easeOut' }}
            className="flex min-w-0 flex-col gap-5"
          >
            <section className="relative overflow-hidden rounded-2xl ring-1 ring-moon-white/15">
              <img
                src={bgImage}
                alt="星樱小镇全景"
                className="h-64 w-full object-cover object-[50%_32%] sm:h-72 lg:h-80"
                draggable={false}
              />
              <div className="absolute inset-0 bg-gradient-to-r from-abyss/75 via-abyss/25 to-transparent" />
              <CharacterDisplay className="absolute -bottom-2 right-3 hidden h-[115%] w-44 sm:block lg:w-52" />
              <div className="absolute inset-x-0 bottom-0 p-5 sm:p-6">
                <p className="flex items-center gap-2 text-[11px] font-semibold tracking-[0.4em] text-sakura">
                  <Sparkles className="h-3.5 w-3.5" aria-hidden="true" />
                  限时活动进行中
                </p>
                <h2 className="mt-2 font-display text-[30px] font-semibold leading-tight text-moon-white drop-shadow-[0_2px_16px_rgba(6,13,36,0.9)] sm:text-[36px]">
                  星樱祭 · 樱落之时
                </h2>
                <p className="mt-1 max-w-md text-[13.5px] leading-relaxed text-silver">
                  当最后一片樱花落下，神社的灯火将为旅人点亮。参与活动，获得限定月白服饰。
                </p>
                <button
                  type="button"
                  onClick={() => window.alert('演示模式：活动详情页尚未上线。')}
                  className="btn-ultra mt-4 rounded-lg px-5 py-2.5 text-[13px] font-semibold tracking-[0.24em] text-moon-white"
                >
                  查看详情
                </button>
              </div>
            </section>

            <section className="glass-panel rounded-2xl p-6">
              <h3 className="font-display text-[20px] font-semibold tracking-[0.16em] text-moon-white">
                关于星樱世界
              </h3>
              <div className="hairline my-4" aria-hidden="true" />
              <p className="text-[14.5px] leading-[1.9] text-silver">
                在群青色的星空下，群山之间藏着一座被时间遗忘的小镇。镇中央的神社
                供奉着星海的记忆，巨大的樱花树每逢月圆便会落下光之花瓣。神社的巫女
                澪在月夜下等待着旅人的到来——据说，与她相遇的人，将在星海的尽头
                找回自己失去的约定。
              </p>
              <p className="mt-3 flex items-center gap-2 text-[12px] tracking-[0.3em] text-ultra-moon">
                <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
                星の彼方、桜の約束 — 在群青色的星海尽头，与你再次相遇。
              </p>
            </section>
          </motion.div>

          {/* Right — download / launch */}
          <motion.div
            initial={{ opacity: 0, x: 18 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ duration: 0.55, delay: 0.16, ease: 'easeOut' }}
          >
            <DownloadPanel />
          </motion.div>
        </div>
      </main>

      <StatusBar />
    </div>
  );
}
