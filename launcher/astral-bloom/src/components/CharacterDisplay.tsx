import { motion } from 'framer-motion';
import heroine from '../assets/characters/heroine.png';

interface CharacterDisplayProps {
  className?: string;
}

/**
 * The heroine — original generated art, cut out to a transparent PNG and
 * layered independently over the background (never baked into it).
 * Moon-white + ultramarine kimono, silver-white hair, a few small sakura
 * ornaments. Restrained lighting: a faint cool rim light from the upper
 * left, matching the moon in the scene. Gentle breathing only.
 * The face sits in the upper third and must never be covered by UI —
 * the login panel is placed below / beside her.
 */
export default function CharacterDisplay({ className = '' }: CharacterDisplayProps) {
  return (
    <div className={`pointer-events-none relative ${className}`} aria-hidden="true">
      {/* Faint cool rim-light halo behind her */}
      <div
        className="animate-glow-pulse absolute inset-x-[8%] top-[6%] bottom-[4%] rounded-full"
        style={{
          background:
            'radial-gradient(50% 55% at 50% 42%, rgba(120,151,219,0.16), rgba(120,151,219,0.05) 60%, transparent 78%)',
          filter: 'blur(6px)',
        }}
      />
      {/* Breathing */}
      <motion.div
        className="relative h-full w-full"
        animate={{ y: [0, -7, 0] }}
        transition={{ duration: 6.5, repeat: Infinity, ease: 'easeInOut' }}
      >
        <img
          src={heroine}
          alt=""
          draggable={false}
          className="h-full w-full object-contain"
          style={{
            filter: 'drop-shadow(0 0 24px rgba(65,105,201,0.25))',
            maskImage:
              'linear-gradient(to bottom, black 84%, transparent 99%)',
            WebkitMaskImage:
              'linear-gradient(to bottom, black 84%, transparent 99%)',
          }}
        />
      </motion.div>
    </div>
  );
}
