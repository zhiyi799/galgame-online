import { motion } from 'framer-motion';
import { Flower2, History, Image, LogOut, Settings } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';

export interface MenuItem {
  key: string;
  en: string;
  zh: string;
  icon: LucideIcon;
}

export const MENU_ITEMS: MenuItem[] = [
  { key: 'start', en: 'GAME START', zh: '开始游戏', icon: Flower2 },
  { key: 'continue', en: 'CONTINUE', zh: '继续游戏', icon: History },
  { key: 'config', en: 'CONFIGURATION', zh: '游戏设置', icon: Settings },
  { key: 'gallery', en: 'GALLERY', zh: 'CG鉴赏', icon: Image },
  { key: 'exit', en: 'EXIT', zh: '退出游戏', icon: LogOut },
];

interface GameMenuProps {
  onSelect: (key: string) => void;
}

/**
 * Restrained visual-novel menu: text, thin hairlines, small icons.
 * No rectangular boxes, no icon chips, no glass — hierarchy comes from
 * typography, spacing and a single marker on the selected entry.
 * GAME START is the selected entry; hover brightens and shifts slightly.
 */
export default function GameMenu({ onSelect }: GameMenuProps) {
  return (
    <nav aria-label="游戏菜单" className="flex flex-col">
      {MENU_ITEMS.map((item, i) => {
        const Icon = item.icon;
        const active = item.key === 'start';
        return (
          <motion.button
            key={item.key}
            type="button"
            initial={{ opacity: 0, x: -18 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ delay: 0.45 + i * 0.09, duration: 0.5, ease: 'easeOut' }}
            whileHover={{ x: 6 }}
            onClick={() => onSelect(item.key)}
            className="group flex items-baseline gap-3 border-b border-moon-white/10 py-3 text-left transition-colors duration-300 first:border-t"
          >
            {/* Selection marker — only the active entry carries one */}
            <span
              className={`w-3 shrink-0 text-[10px] leading-none ${
                active ? 'text-sakura' : 'text-transparent'
              }`}
              aria-hidden="true"
            >
              ✦
            </span>
            <Icon
              className={`h-4 w-4 shrink-0 self-center transition-colors duration-300 ${
                active ? 'text-ultra-moon' : 'text-silver/55 group-hover:text-silver'
              }`}
              aria-hidden="true"
            />
            <span className="leading-tight">
              <span
                className={`block font-display text-[19px] font-semibold tracking-[0.22em] transition-colors duration-300 ${
                  active ? 'text-moon-white' : 'text-silver/80 group-hover:text-moon-white'
                }`}
              >
                {item.en}
              </span>
              <span
                className={`mt-0.5 block text-[11px] tracking-[0.32em] transition-colors duration-300 ${
                  active ? 'text-silver' : 'text-silver/50 group-hover:text-silver/75'
                }`}
              >
                {item.zh}
              </span>
            </span>
          </motion.button>
        );
      })}
    </nav>
  );
}
