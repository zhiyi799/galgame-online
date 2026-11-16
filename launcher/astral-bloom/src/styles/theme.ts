/**
 * ASTRAL BLOOM — 星樱物语 · design tokens.
 * Single source of truth for the ultramarine visual system.
 * Ultramarine is the ENVIRONMENT colour of the whole world, not just
 * buttons and borders: background = ultramarine night sky, character
 * wears moon-white + ultramarine, sakura pink is only a small accent.
 */
export const palette = {
  /** Deep ultramarine — main dark, ~55% of the visual */
  deepUltramarine: '#142B67',
  /** Classic ultramarine — primary actions */
  classicUltramarine: '#244DAB',
  /** Starry blue — hover / glow */
  starryBlue: '#4169C9',
  /** Moonlight blue — highlights / accents */
  moonlightBlue: '#7897DB',
  /** Moon white — primary text */
  moonWhite: '#E5EAF7',
  /** Silver white — secondary text / hairlines */
  silverWhite: '#C9D4E8',
  /** Sakura pink — small accent only (~10% with pale gold) */
  sakuraPink: '#DDAFCB',
  /** Pale gold — small accent only */
  paleGold: '#D9CBAA',
  /** Night — deepest background */
  night: '#0A1633',
  /** Abyss — page base */
  abyss: '#060D24',
} as const;

export type PaletteKey = keyof typeof palette;

/** Intended visual proportions of the whole scene. */
export const colorRatio = {
  ultramarine: 0.55,
  deepBlueGrey: 0.2,
  moonSilverWhite: 0.15,
  sakuraPinkAndPaleGold: 0.1,
} as const;

export const GAME = {
  titleEn: 'ASTRAL BLOOM',
  titleZh: '星樱物语',
  subtitleJp: '星の彼方、桜の約束',
  tagline: '在群青色的星海尽头，与你再次相遇。',
  version: '1.0.0',
  gameVersion: '1.0.0',
  buildSizeGb: 18.6,
  launcherVersion: '1.0.0',
} as const;

export const DEMO = {
  /** Demo login: any well-formed email works locally. Never real verification. */
  demoAccountHint: '演示模式 · 任意邮箱均可登录（本地演示，不会验证或上传真实账号信息）',
  demoDownloadHint: '模拟下载 · 演示数据，非真实文件传输',
  demoServerHint: '演示服务器 · 模拟在线状态',
} as const;
