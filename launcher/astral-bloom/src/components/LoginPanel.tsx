import { useState } from 'react';
import type { FormEvent } from 'react';
import { motion } from 'framer-motion';
import { CheckCircle2, Eye, EyeOff, Loader2, Lock, Sparkles, User, XCircle } from 'lucide-react';
import type { AuthStatus } from '../hooks/useAuth';
import { DEMO } from '../styles/theme';

interface LoginPanelProps {
  status: AuthStatus;
  error: string;
  /** Remembered account name from a previous visit (pre-filled). */
  initialUsername?: string;
  onLogin: (username: string, password: string, remember: boolean) => void;
}

const STATUS_TEXT: Record<AuthStatus, string> = {
  idle: 'PLEASE SIGN IN TO CONTINUE YOUR JOURNEY.',
  verifying: '正在连接星门，验证旅人身份…',
  success: '欢迎回来，旅人。星樱之门已为你开启。',
  failed: '登录失败，请检查账号与密码后重试。',
};

/**
 * Login panel — restrained, like a real game client: deep dark-navy,
 * slight blur, thin border. No heavy glassmorphism, no third-party login
 * (standalone account only). Sits on the right and never covers the
 * character's face (the character's face is in the upper third; the panel
 * is anchored low). Demo auth only: any account works locally; this is
 * labelled as a demo and never pretends to be real account verification.
 */
export default function LoginPanel({ status, error, initialUsername = '', onLogin }: LoginPanelProps) {
  const [username, setUsername] = useState(initialUsername);
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [remember, setRemember] = useState(true);
  const [autoLogin, setAutoLogin] = useState(false);
  const [hint, setHint] = useState('');

  const busy = status === 'verifying' || status === 'success';

  const submit = (e: FormEvent) => {
    e.preventDefault();
    if (busy) return;
    setHint('');
    onLogin(username, password, remember || autoLogin);
  };

  return (
    <motion.section
      initial={{ opacity: 0, y: 26 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: 0.55, duration: 0.7, ease: 'easeOut' }}
      className="login-panel relative w-full max-w-md rounded-xl px-7 py-7 sm:px-8"
      aria-label="登录面板"
    >
      <header className="text-center">
        <p className="flex items-center justify-center gap-2 text-[11px] font-semibold tracking-[0.42em] text-ultra-moon">
          <Sparkles className="h-3.5 w-3.5" aria-hidden="true" />
          WELCOME BACK
          <Sparkles className="h-3.5 w-3.5" aria-hidden="true" />
        </p>
        <h2 className="mt-2 font-display text-[26px] font-semibold tracking-[0.12em] text-moon-white">
          欢迎回到星樱世界
        </h2>
        <p className="mt-1.5 text-[12px] tracking-[0.18em] text-silver/75">
          {STATUS_TEXT[status]}
        </p>
        {initialUsername && (
          <p className="mt-1 text-[12px] text-ultra-moon">
            上次登录：{initialUsername} · 输入密码即可继续
          </p>
        )}
      </header>

      <form onSubmit={submit} className="mt-6 flex flex-col gap-4">
        <label className="block">
          <span className="mb-1.5 block text-[12px] tracking-[0.3em] text-silver">
            邮箱账号
          </span>
          <span className="flex items-center gap-2.5 rounded-lg border border-moon-white/20 bg-night/50 px-3.5 py-3 transition-colors focus-within:border-ultra-moon focus-within:shadow-[0_0_0_3px_rgba(65,105,201,0.25)]">
            <User className="h-4 w-4 shrink-0 text-ultra-moon" aria-hidden="true" />
            <input
              id="login-username"
              type="email"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="请输入邮箱"
              autoComplete="email"
              disabled={busy}
              className="w-full bg-transparent text-[15px] text-moon-white placeholder:text-silver/45 focus:outline-none disabled:opacity-60"
            />
          </span>
        </label>

        <label className="block">
          <span className="mb-1.5 block text-[12px] tracking-[0.3em] text-silver">
            登录密码
          </span>
          <span className="flex items-center gap-2.5 rounded-lg border border-moon-white/20 bg-night/50 px-3.5 py-3 transition-colors focus-within:border-ultra-moon focus-within:shadow-[0_0_0_3px_rgba(65,105,201,0.25)]">
            <Lock className="h-4 w-4 shrink-0 text-ultra-moon" aria-hidden="true" />
            <input
              type={showPassword ? 'text' : 'password'}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="请输入登录密码"
              autoComplete="current-password"
              disabled={busy}
              className="w-full bg-transparent text-[15px] text-moon-white placeholder:text-silver/45 focus:outline-none disabled:opacity-60"
            />
            <button
              type="button"
              onClick={() => setShowPassword((v) => !v)}
              className="shrink-0 text-silver/70 transition-colors hover:text-moon-white"
              aria-label={showPassword ? '隐藏密码' : '显示密码'}
            >
              {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
            </button>
          </span>
        </label>

        <div className="flex items-center justify-between text-[13px] text-silver/85">
          <label className="flex cursor-pointer items-center gap-2">
            <input
              type="checkbox"
              checked={remember}
              onChange={(e) => setRemember(e.target.checked)}
              className="h-3.5 w-3.5 accent-[#4169C9]"
            />
            记住账号
          </label>
          <label className="flex cursor-pointer items-center gap-2">
            <input
              type="checkbox"
              checked={autoLogin}
              onChange={(e) => setAutoLogin(e.target.checked)}
              className="h-3.5 w-3.5 accent-[#4169C9]"
            />
            自动登录
          </label>
          <button
            type="button"
            className="text-ultra-moon transition-colors hover:text-moon-white"
            onClick={() => setHint('演示模式：忘记密码可直接使用任意邮箱登录，无需找回。')}
          >
            忘记密码？
          </button>
        </div>

        {status === 'failed' && (
          <p role="alert" className="flex items-center gap-2 rounded-lg border border-sakura/40 bg-sakura/10 px-3 py-2.5 text-[13px] text-sakura">
            <XCircle className="h-4 w-4 shrink-0" aria-hidden="true" />
            {error}
          </p>
        )}
        {status === 'success' && (
          <p role="status" className="flex items-center gap-2 rounded-lg border border-ultra-moon/40 bg-ultra-starry/15 px-3 py-2.5 text-[13px] text-moon-white">
            <CheckCircle2 className="h-4 w-4 shrink-0" aria-hidden="true" />
            登录成功，正在进入星樱世界…
          </p>
        )}
        {hint && status !== 'failed' && (
          <p className="rounded-lg border border-pale-gold/30 bg-pale-gold/10 px-3 py-2.5 text-[13px] text-pale-gold">
            {hint}
          </p>
        )}

        <button
          type="submit"
          disabled={busy}
          className="btn-ultra mt-1 rounded-lg py-3 text-[15px] font-semibold text-moon-white disabled:opacity-70"
        >
          {status === 'verifying' ? (
            <span className="flex items-center justify-center gap-2">
              <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
              VERIFYING…
            </span>
          ) : status === 'success' ? (
            <span className="flex items-center justify-center gap-2">
              <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
              WELCOME
            </span>
          ) : (
            <span className="flex flex-col items-center leading-tight">
              <span className="tracking-[0.34em]">ENTER THE WORLD</span>
              <span className="mt-0.5 text-[12px] font-normal tracking-[0.5em] text-moon-white/85">
                进入游戏
              </span>
            </span>
          )}
        </button>

        <p className="text-center text-[13px] text-silver/75">
          还没有账号？
          <button
            type="button"
            className="ml-1 font-medium text-ultra-moon transition-colors hover:text-moon-white"
            onClick={() => setHint('演示模式：邮箱注册即登录 — 直接输入任意邮箱与密码即可进入。')}
          >
            立即注册
          </button>
        </p>

        <div className="hairline" aria-hidden="true" />
        <p className="text-center text-[11.5px] leading-relaxed text-silver/60">
          {DEMO.demoAccountHint}
        </p>
      </form>
    </motion.section>
  );
}
