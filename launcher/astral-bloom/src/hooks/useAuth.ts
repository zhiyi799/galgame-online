import { useCallback, useState } from 'react';

export type AuthStatus = 'idle' | 'verifying' | 'success' | 'failed';

export interface DemoUser {
  username: string;
  nickname: string;
}

const STORAGE_KEY = 'astral-bloom-user';

function readStored(): DemoUser | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as DemoUser) : null;
  } catch {
    return null;
  }
}

/**
 * Demo authentication — LOCAL ONLY, email + Steam only (independent
 * launcher: QQ / WeChat third-party login removed).
 * Any well-formed email address with a password of 4+ characters signs
 * in. Nothing is verified against a server and nothing is uploaded;
 * this is a launcher demo, not real account verification.
 *
 * `user` is the active session; `stored` is the remembered account from a
 * previous visit (only set when "记住账号" was checked). The app may
 * restore the stored account automatically when the auto-login setting
 * is on, otherwise the login page pre-fills the remembered name.
 */
export function useAuth() {
  const [user, setUser] = useState<DemoUser | null>(null);
  const [stored, setStored] = useState<DemoUser | null>(() => readStored());
  const [status, setStatus] = useState<AuthStatus>('idle');
  const [error, setError] = useState<string>('');

  const login = useCallback((username: string, password: string, remember: boolean) => {
    const name = username.trim();
    // Demo email login: accept any well-formed email address locally.
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(name)) {
      setError('请输入有效的邮箱地址（演示模式）。');
      setStatus('failed');
      return;
    }
    if (password.length < 4) {
      setError('密码至少 4 位（演示模式）。');
      setStatus('failed');
      return;
    }
    setError('');
    setStatus('verifying');
    window.setTimeout(() => {
      const next: DemoUser = { username: name, nickname: name };
      setUser(next);
      setStatus('success');
      try {
        if (remember) {
          localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
          setStored(next);
        } else {
          localStorage.removeItem(STORAGE_KEY);
          setStored(null);
        }
      } catch {
        /* storage unavailable — session only */
      }
    }, 900);
  }, []);

  const restoreStored = useCallback(() => {
    const s = readStored();
    if (s) {
      setUser(s);
      setStatus('success');
    }
  }, []);

  const logout = useCallback(() => {
    setUser(null);
    setStatus('idle');
    setError('');
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      /* ignore */
    }
    setStored(null);
  }, []);

  return { user, stored, status, error, login, logout, restoreStored, setStatus };
}
