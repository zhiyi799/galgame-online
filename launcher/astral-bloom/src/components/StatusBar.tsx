import { GAME, DEMO } from '../styles/theme';

/**
 * Bottom status bar — matches the concept mockup:
 * left "Ver.1.0.0 | ASTRAL BLOOM", right server + online count.
 * The server / online numbers are DEMO placeholder data, labelled as such.
 */
export default function StatusBar() {
  return (
    <footer className="relative z-30 flex h-9 shrink-0 items-center justify-between gap-4 border-t border-moon-white/10 bg-abyss/45 px-4 text-[11.5px] tracking-wider text-silver/75 backdrop-blur-md sm:px-6 lg:px-10">
      <span className="flex items-center gap-2">
        <span className="text-ultra-moon" aria-hidden="true">◆</span>
        Ver.{GAME.version}
        <span className="text-silver/45">|</span>
        ASTRAL BLOOM
      </span>
      <span className="flex items-center gap-4">
        <span className="flex items-center gap-1.5">
          <span className="relative flex h-2 w-2">
            <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-ultra-moon opacity-60" />
            <span className="relative inline-flex h-2 w-2 rounded-full bg-ultra-moon" />
          </span>
          服务器：星海之境
          <span className="text-silver/45">⌄</span>
        </span>
        <span>
          在线人数：12,345
          <span className="ml-1 text-silver/45">（{DEMO.demoServerHint}）</span>
        </span>
      </span>
    </footer>
  );
}
