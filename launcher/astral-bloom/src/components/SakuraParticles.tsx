import { useEffect, useRef } from 'react';

interface Star {
  x: number;
  y: number;
  r: number;
  phase: number;
  speed: number;
}

interface Petal {
  x: number;
  y: number;
  size: number;
  speedY: number;
  sway: number;
  swaySpeed: number;
  phase: number;
  rot: number;
  rotSpeed: number;
  pink: boolean;
  alpha: number;
}

const SAKURA = '221, 175, 203';
const MOON = '229, 234, 247';

/**
 * Canvas particle layer: twinkling stars + slowly falling sakura petals.
 * - Counts are capped (desktop ≤ 42 petals / 90 stars, small screens fewer).
 * - transform/opacity only; a single rAF loop, paused when the tab hides.
 * - Respects prefers-reduced-motion: draws one static frame, no animation.
 */
export default function SakuraParticles() {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    let w = 0;
    let h = 0;
    let raf = 0;
    let running = true;

    const resize = () => {
      const rect = canvas.getBoundingClientRect();
      w = rect.width;
      h = rect.height;
      canvas.width = Math.max(1, Math.floor(w * dpr));
      canvas.height = Math.max(1, Math.floor(h * dpr));
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    };
    resize();

    const small = w < 768;
    const starCount = small ? 45 : 90;
    const petalCount = small ? 18 : 42;

    const stars: Star[] = Array.from({ length: starCount }, () => ({
      x: Math.random() * w,
      y: Math.random() * h * 0.55,
      r: 0.6 + Math.random() * 1.4,
      phase: Math.random() * Math.PI * 2,
      speed: 0.4 + Math.random() * 0.9,
    }));

    const petals: Petal[] = Array.from({ length: petalCount }, () => ({
      x: Math.random() * w,
      y: Math.random() * h,
      size: 5 + Math.random() * 7,
      speedY: 0.25 + Math.random() * 0.55,
      sway: 18 + Math.random() * 26,
      swaySpeed: 0.4 + Math.random() * 0.7,
      phase: Math.random() * Math.PI * 2,
      rot: Math.random() * Math.PI * 2,
      rotSpeed: (Math.random() - 0.5) * 0.02,
      pink: Math.random() < 0.72,
      alpha: 0.45 + Math.random() * 0.45,
    }));

    const drawPetal = (p: Petal) => {
      ctx.save();
      ctx.translate(p.x, p.y);
      ctx.rotate(p.rot);
      ctx.fillStyle = `rgba(${p.pink ? SAKURA : MOON}, ${p.alpha})`;
      ctx.beginPath();
      ctx.ellipse(0, 0, p.size, p.size * 0.62, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    };

    const draw = (t: number) => {
      ctx.clearRect(0, 0, w, h);
      // Stars
      for (const s of stars) {
        const a = 0.25 + 0.65 * (0.5 + 0.5 * Math.sin(t * 0.001 * s.speed + s.phase));
        ctx.fillStyle = `rgba(229, 234, 247, ${a.toFixed(3)})`;
        ctx.beginPath();
        ctx.arc(s.x, s.y, s.r, 0, Math.PI * 2);
        ctx.fill();
      }
      // Petals
      for (const p of petals) {
        p.y += p.speedY;
        p.phase += 0.008 * p.swaySpeed;
        p.rot += p.rotSpeed;
        p.x += Math.sin(p.phase) * 0.35;
        if (p.y > h + 16) {
          p.y = -16;
          p.x = Math.random() * w;
        }
        if (p.x < -20) p.x = w + 16;
        if (p.x > w + 20) p.x = -16;
        drawPetal(p);
      }
    };

    const drawStatic = () => {
      ctx.clearRect(0, 0, w, h);
      for (const s of stars) {
        ctx.fillStyle = 'rgba(229, 234, 247, 0.6)';
        ctx.beginPath();
        ctx.arc(s.x, s.y, s.r, 0, Math.PI * 2);
        ctx.fill();
      }
      for (const p of petals) drawPetal(p);
    };

    if (reduced) {
      drawStatic();
      return () => {
        running = false;
      };
    }

    const loop = (t: number) => {
      if (!running) return;
      draw(t);
      raf = requestAnimationFrame(loop);
    };
    raf = requestAnimationFrame(loop);

    const onVisibility = () => {
      if (document.hidden) {
        cancelAnimationFrame(raf);
      } else if (running) {
        raf = requestAnimationFrame(loop);
      }
    };
    document.addEventListener('visibilitychange', onVisibility);
    window.addEventListener('resize', resize);

    return () => {
      running = false;
      cancelAnimationFrame(raf);
      document.removeEventListener('visibilitychange', onVisibility);
      window.removeEventListener('resize', resize);
    };
  }, []);

  return (
    <canvas
      ref={canvasRef}
      className="pointer-events-none absolute inset-0 h-full w-full"
      aria-hidden="true"
    />
  );
}
