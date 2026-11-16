import { useEffect, useState } from 'react';

/**
 * Mouse parallax for the background scene.
 * Returns a small pixel offset driven by the pointer, throttled through
 * requestAnimationFrame. Returns {0,0} when the user prefers reduced
 * motion. Safe to call on any page; listens on window.
 */
export function useParallax(strength = 14): { x: number; y: number } {
  const [offset, setOffset] = useState({ x: 0, y: 0 });

  useEffect(() => {
    if (
      typeof window === 'undefined' ||
      window.matchMedia('(prefers-reduced-motion: reduce)').matches
    ) {
      return;
    }
    let raf = 0;
    const onMove = (e: MouseEvent) => {
      cancelAnimationFrame(raf);
      raf = requestAnimationFrame(() => {
        setOffset({
          x: (e.clientX / window.innerWidth - 0.5) * strength,
          y: (e.clientY / window.innerHeight - 0.5) * strength,
        });
      });
    };
    window.addEventListener('mousemove', onMove, { passive: true });
    return () => {
      window.removeEventListener('mousemove', onMove);
      cancelAnimationFrame(raf);
    };
  }, [strength]);

  return offset;
}
