import { useEffect, useState } from 'react';
import { AnimatePresence, motion } from 'framer-motion';
import LoginPage from './pages/LoginPage';
import DashboardPage from './pages/DashboardPage';
import SettingsModal from './components/SettingsModal';
import { useAuth } from './hooks/useAuth';
import { useSettings } from './hooks/useSettings';

/**
 * ASTRAL BLOOM — 星樱物语 · game launcher.
 * Two screens: the login page (brand + menu + character + login panel)
 * and, after a demo sign-in, the launcher dashboard (promo, updates,
 * simulated download / launch). Settings live above both screens.
 */
export default function App() {
  const auth = useAuth();
  const { settings, update } = useSettings();
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [entered, setEntered] = useState(false);

  // Auto-login: restore a remembered account on launch when enabled.
  useEffect(() => {
    if (auth.stored && settings.autoLogin && !auth.user) {
      auth.restoreStored();
    }
    // Once, on mount.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Elegant transition: let the "登录成功" state breathe before the
  // dashboard fades in.
  useEffect(() => {
    if (auth.user) {
      const t = window.setTimeout(() => setEntered(true), 1100);
      return () => window.clearTimeout(t);
    }
    setEntered(false);
    return undefined;
  }, [auth.user]);

  const showDashboard = entered && auth.user !== null;

  const pageProps = {
    musicEnabled: settings.musicEnabled,
    language: settings.language,
    onLanguageChange: (lang: typeof settings.language) => update({ language: lang }),
    onToggleMusic: () => update({ musicEnabled: !settings.musicEnabled }),
    onOpenSettings: () => setSettingsOpen(true),
  };

  return (
    <div className="h-full">
      <AnimatePresence mode="wait">
        {showDashboard && auth.user ? (
          <motion.div
            key="dashboard"
            className="h-full"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.6, ease: 'easeOut' }}
          >
            <DashboardPage user={auth.user} onLogout={auth.logout} {...pageProps} />
          </motion.div>
        ) : (
          <motion.div
            key="login"
            className="h-full"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.6, ease: 'easeOut' }}
          >
            <LoginPage auth={auth} {...pageProps} />
          </motion.div>
        )}
      </AnimatePresence>

      <SettingsModal
        open={settingsOpen}
        onClose={() => setSettingsOpen(false)}
        settings={settings}
        onChange={update}
      />
    </div>
  );
}
