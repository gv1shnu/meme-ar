import { placeAt, type Layout } from '../analysis/placement';
import type { PlannedReaction, TrackedFrame } from '../analysis/types';
import type { Sticker } from '../stickers/loader';

export interface ScheduledSticker {
  reaction: PlannedReaction;
  layout: Layout;
  sticker: Sticker;
}

const IN = 0.22;
const OUT = 0.25;

const easeOutBack = (x: number) => {
  const c1 = 1.70158;
  const c3 = c1 + 1;
  return 1 + c3 * Math.pow(x - 1, 3) + c1 * Math.pow(x - 1, 2);
};

/** Scale, opacity and tilt for a sticker `local` seconds into its lifetime. */
export function animationAt(local: number, duration: number, timing: PlannedReaction['timing']) {
  const enter = Math.min(1, local / IN);
  const exit = Math.min(1, Math.max(0, (duration - local) / OUT));
  const scale = (0.3 + 0.7 * easeOutBack(enter)) * (0.85 + 0.15 * exit);
  const alpha = Math.min(enter * 1.5, 1) * exit;
  const wobble = timing === 'instant' ? 0.06 : 0.03;
  const rotate = Math.sin(local * 5.5) * wobble * (1 - 0.5 * Math.min(1, local / 1.5));
  return { scale, alpha, rotate };
}

/**
 * Draws every sticker active at time `t` onto a context whose coordinate space
 * is video pixels. Used by both the live preview and the exporter, so what you
 * see is exactly what you download.
 */
export function drawStickers(
  ctx: CanvasRenderingContext2D,
  items: ScheduledSticker[],
  frames: TrackedFrame[],
  t: number,
  videoW: number,
  videoH: number,
) {
  for (const { reaction: r, layout, sticker } of items) {
    const local = t - r.start;
    if (local < 0 || local > r.duration) continue;

    const rect = placeAt(frames, r, layout, t, videoW, videoH, sticker.width / sticker.height);
    const { scale, alpha, rotate } = animationAt(local, r.duration, r.timing);
    if (alpha <= 0.01) continue;

    ctx.save();
    ctx.globalAlpha = alpha;
    ctx.translate(rect.x + rect.w / 2, rect.y + rect.h / 2);
    ctx.rotate(rotate);
    ctx.scale(scale, scale);
    ctx.shadowColor = 'rgba(0,0,0,0.35)';
    ctx.shadowBlur = Math.min(videoW, videoH) * 0.015;
    ctx.drawImage(sticker.frameAt(local * 1000), -rect.w / 2, -rect.h / 2, rect.w, rect.h);
    ctx.restore();
  }
}
