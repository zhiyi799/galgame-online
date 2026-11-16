import { useEffect, useRef, useState } from 'react';
import { CheckCircle2, Download, Globe, HardDrive, Pause, Play, Rocket, ShieldCheck, X } from 'lucide-react';
import { DEMO, GAME } from '../styles/theme';

type DownloadState = 'idle' | 'downloading' | 'paused' | 'verifying' | 'complete';

const REGIONS = ['星樱服 · 亚服', '星樱服 · 国服', '星樱服 · 国际服'] as const;

/**
 * Download / launch panel.
 * Web demo: the download is SIMULATED with a timer and clearly labelled
 * as such — it never pretends to transfer real files. A desktop client
 * would replace the simulation with real install/download/launch calls
 * behind the same interface (see the `// DESKTOP:` notes in the code).
 */
export default function DownloadPanel() {
  const [installed, setInstalled] = useState(false);
  const [state, setState] = useState<DownloadState>('idle');
  const [progress, setProgress] = useState(0); // 0–100
  const [speed, setSpeed] = useState(0); // MB/s, simulated
  const [region, setRegion] = useState<(typeof REGIONS)[number]>(REGIONS[0]);
  const [fullscreen, setFullscreen] = useState(true);
  const [skipIntro, setSkipIntro] = useState(false);
  const [integrity, setIntegrity] = useState<'pending' | 'checking' | 'passed'>('pending');
  const timer = useRef<number | null>(null);

  const totalGb = GAME.buildSizeGb;
  const downloadedGb = (totalGb * progress) / 100;

  useEffect(() => {
    if (state !== 'downloading') return;
    timer.current = window.setInterval(() => {
      // Simulated throughput, 18–42 MB/s, so a full "download" is quick.
      const mbps = 18 + Math.random() * 24;
      setSpeed(mbps);
      setProgress((p) => {
        const next = p + (mbps * 0.5 * 100) / (totalGb * 1024);
        if (next >= 100) {
          setState('verifying');
          return 100;
        }
        return next;
      });
    }, 500);
    return () => {
      if (timer.current) window.clearInterval(timer.current);
    };
  }, [state, totalGb]);

  useEffect(() => {
    if (state !== 'verifying') return;
    setIntegrity('checking');
    const t = window.setTimeout(() => {
      setIntegrity('passed');
      setInstalled(true);
      setState('complete');
    }, 1400);
    return () => window.clearTimeout(t);
  }, [state]);

  const startDownload = () => {
    setIntegrity('pending');
    setProgress(0);
    setState('downloading');
  };
  const pause = () => setState('paused');
  const resume = () => setState('downloading');
  const cancel = () => {
    setState('idle');
    setProgress(0);
    setSpeed(0);
  };

  // DESKTOP: replace with the real launcher API, e.g.
  // window.astral?.launchGame({ region, fullscreen, skipIntro })
  const launchGame = () => {
    window.alert(
      `演示模式：游戏启动接口已预留。\n区域：${region}\n全屏：${fullscreen ? '是' : '否'}\n跳过开场：${skipIntro ? '是' : '否'}`,
    );
  };

  return (
    <section className="glass-panel rounded-2xl p-5" aria-label="游戏下载与启动">
      <div className="flex items-center justify-between gap-3">
        <h3 className="font-display text-[18px] font-semibold tracking-[0.18em] text-moon-white">
          {installed ? 'START GAME' : 'DOWNLOAD GAME'}
        </h3>
        <span className="rounded-full border border-ultra-moon/40 bg-ultra-starry/20 px-2.5 py-1 text-[11px] tracking-wider text-ultra-moon">
          {DEMO.demoDownloadHint}
        </span>
      </div>

      {/* Version / region / integrity */}
      <div className="mt-4 grid grid-cols-1 gap-2.5 text-[13px] sm:grid-cols-3">
        <p className="flex items-center gap-2 rounded-lg bg-night/45 px-3 py-2.5 text-silver">
          <HardDrive className="h-4 w-4 shrink-0 text-ultra-moon" aria-hidden="true" />
          游戏版本 v{GAME.gameVersion}
          <span className="ml-auto text-[11px] text-ultra-moon">最新</span>
        </p>
        <label className="flex items-center gap-2 rounded-lg bg-night/45 px-3 py-2.5 text-silver">
          <Globe className="h-4 w-4 shrink-0 text-ultra-moon" aria-hidden="true" />
          <select
            value={region}
            onChange={(e) => setRegion(e.target.value as (typeof REGIONS)[number])}
            className="w-full cursor-pointer bg-transparent text-[13px] text-moon-white outline-none [&>option]:bg-ultra-deep"
            aria-label="服务器区域"
          >
            {REGIONS.map((r) => (
              <option key={r} value={r}>
                {r}
              </option>
            ))}
          </select>
        </label>
        <p className="flex items-center gap-2 rounded-lg bg-night/45 px-3 py-2.5 text-silver">
          <ShieldCheck className="h-4 w-4 shrink-0 text-ultra-moon" aria-hidden="true" />
          文件完整性：
          <span className="text-moon-white">
            {integrity === 'passed' ? '已通过（模拟）' : integrity === 'checking' ? '校验中…' : '待校验'}
          </span>
        </p>
      </div>

      {/* Progress */}
      {(state === 'downloading' || state === 'paused' || state === 'verifying') && (
        <div className="mt-4">
          <div className="flex items-center justify-between text-[12.5px] text-silver">
            <span>
              {state === 'verifying'
                ? '正在校验文件完整性…'
                : `已下载 ${downloadedGb.toFixed(2)} GB / ${totalGb} GB`}
            </span>
            <span className="text-moon-white">
              {state === 'downloading' ? `${speed.toFixed(1)} MB/s · ` : ''}
              {progress.toFixed(1)}%
            </span>
          </div>
          <div className="mt-2 h-2.5 overflow-hidden rounded-full bg-night/70 ring-1 ring-moon-white/15">
            <div
              className="h-full rounded-full bg-gradient-to-r from-ultra-classic via-ultra-starry to-ultra-moon transition-[width] duration-500"
              style={{ width: `${progress}%` }}
            />
          </div>
        </div>
      )}

      {/* Launch options */}
      <div className="mt-4 flex flex-wrap items-center gap-x-5 gap-y-2 text-[13px] text-silver/85">
        <label className="flex cursor-pointer items-center gap-2">
          <input type="checkbox" checked={fullscreen} onChange={(e) => setFullscreen(e.target.checked)} className="h-3.5 w-3.5 accent-[#4169C9]" />
          全屏启动
        </label>
        <label className="flex cursor-pointer items-center gap-2">
          <input type="checkbox" checked={skipIntro} onChange={(e) => setSkipIntro(e.target.checked)} className="h-3.5 w-3.5 accent-[#4169C9]" />
          跳过开场动画
        </label>
        <span className="ml-auto hidden text-[12px] text-silver/55 sm:block">
          需要 {totalGb} GB 可用空间
        </span>
      </div>

      {/* Main action */}
      <div className="mt-5 flex flex-wrap items-center gap-3">
        {installed ? (
          <button
            type="button"
            onClick={launchGame}
            className="btn-ultra flex flex-1 items-center justify-center gap-2.5 rounded-xl px-6 py-4 text-[16px] font-semibold tracking-[0.3em] text-moon-white"
          >
            <Rocket className="h-5 w-5" aria-hidden="true" />
            START GAME
          </button>
        ) : state === 'idle' ? (
          <button
            type="button"
            onClick={startDownload}
            className="btn-ultra flex flex-1 items-center justify-center gap-2.5 rounded-xl px-6 py-4 text-[16px] font-semibold tracking-[0.3em] text-moon-white"
          >
            <Download className="h-5 w-5" aria-hidden="true" />
            DOWNLOAD GAME
          </button>
        ) : (
          <>
            <button
              type="button"
              onClick={state === 'paused' ? resume : pause}
              disabled={state === 'verifying'}
              className="btn-ultra flex flex-1 items-center justify-center gap-2.5 rounded-xl px-6 py-4 text-[15px] font-semibold tracking-[0.26em] text-moon-white disabled:opacity-60"
            >
              {state === 'paused' ? <Play className="h-5 w-5" aria-hidden="true" /> : <Pause className="h-5 w-5" aria-hidden="true" />}
              {state === 'verifying' ? 'VERIFYING…' : state === 'paused' ? '继续下载' : '暂停下载'}
            </button>
            <button
              type="button"
              onClick={cancel}
              disabled={state === 'verifying'}
              className="flex items-center gap-2 rounded-xl border border-sakura/50 bg-sakura/10 px-5 py-4 text-[14px] font-medium text-sakura transition-colors hover:bg-sakura/20 disabled:opacity-50"
            >
              <X className="h-4 w-4" aria-hidden="true" />
              取消下载
            </button>
          </>
        )}
      </div>

      {state === 'complete' && (
        <p role="status" className="mt-3 flex items-center gap-2 text-[13px] text-ultra-moon">
          <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
          下载完成，文件完整性校验通过（模拟）。点击 START GAME 启动游戏。
        </p>
      )}
    </section>
  );
}
