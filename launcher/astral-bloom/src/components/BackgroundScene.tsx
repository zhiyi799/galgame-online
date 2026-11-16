import { motion } from 'framer-motion';
import bgImage from '../assets/backgrounds/town-night.webp';
import SakuraParticles from './SakuraParticles';

interface BackgroundSceneProps {
  /** Mouse parallax offset in px. */
  parallax: { x: number; y: number };
  /** Extra dimming for the dashboard (content sits on top). */
  dim?: boolean;
}

/**
 * Full-screen night shrine scene: the painted key visual (shrine hall,
 * torii, stone steps, sakura tree, mountains under a crescent moon) with
 * slow cloud drift, a faint moon glow where the painted moon sits, mouse
 * parallax and a few falling petals. Restrained — no particle storm, no
 * glowing lines; the scene is a believable place, not a poster.
 */
export default function BackgroundScene({ parallax, dim = false }: BackgroundSceneProps) {
  return (
    <div className="absolute inset-0 overflow-hidden bg-abyss" aria-hidden="true">
      {/* Key visual, slightly oversized so parallax never shows edges */}
      <motion.div
        className="absolute -inset-[3%]"
        style={{ willChange: 'transform' }}
        animate={{ x: -parallax.x * 0.6, y: -parallax.y * 0.4 }}
        transition={{ type: 'spring', stiffness: 55, damping: 18 }}
      >
        <img
          src={bgImage}
          alt=""
          className="h-full w-full object-cover"
          draggable={false}
        />
      </motion.div>

      {/* Slow drifting cloud wash */}
      <div
        className="animate-drift absolute inset-0 opacity-40"
        style={{
          background:
            'radial-gradient(60% 28% at 22% 18%, rgba(120,151,219,0.16), transparent 70%), radial-gradient(50% 24% at 78% 12%, rgba(65,105,201,0.14), transparent 70%)',
        }}
      />

      {/* Faint moon glow, upper left where the crescent moon sits in the art */}
      <div
        className="animate-moon-pulse absolute left-[4%] top-[4%] h-56 w-56 rounded-full"
        style={{
          background:
            'radial-gradient(circle, rgba(229,234,247,0.10), rgba(120,151,219,0.05) 45%, transparent 70%)',
        }}
      />

      {/* Legibility gradients: top for the navbar, left for the brand text,
          bottom for the status bar. All ultramarine, no black crush. */}
      <div className="absolute inset-0 bg-gradient-to-b from-abyss/70 via-transparent to-abyss/75" />
      <div className="absolute inset-0 bg-gradient-to-r from-abyss/55 via-abyss/10 to-transparent" />
      {dim && <div className="absolute inset-0 bg-ultra-deep/35 backdrop-blur-[1px]" />}

      {/* Sakura petals + twinkling stars */}
      <SakuraParticles />
    </div>
  );
}
