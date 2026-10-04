import { decompressFrames, parseGIF } from 'gifuct-js';

export interface DecodedAnimation {
  width: number;
  height: number;
  frames: ImageBitmap[];
  /** Cumulative end time (ms) of each frame; last entry is the loop length. */
  ends: number[];
}

const MAX_FRAMES = 90;

/**
 * Decodes an animated GIF into fully composited frames. Canvas `drawImage` on an
 * animated <img> only paints the first frame, so stickers are decoded once and
 * drawn frame-by-frame in both preview and export.
 */
export async function decodeGif(buffer: ArrayBuffer): Promise<DecodedAnimation> {
  const gif = parseGIF(buffer);
  const parsed = decompressFrames(gif, true).slice(0, MAX_FRAMES);
  const { width, height } = gif.lsd;

  const canvas = new OffscreenCanvas(width, height);
  const ctx = canvas.getContext('2d')!;
  const patchCanvas = new OffscreenCanvas(1, 1);
  const patchCtx = patchCanvas.getContext('2d')!;

  const frames: ImageBitmap[] = [];
  const ends: number[] = [];
  let elapsed = 0;
  let previous: ImageData | null = null;

  for (const f of parsed) {
    const { left, top, width: w, height: h } = f.dims;
    if (f.disposalType === 3) previous = ctx.getImageData(0, 0, width, height);

    if (patchCanvas.width !== w || patchCanvas.height !== h) {
      patchCanvas.width = w;
      patchCanvas.height = h;
    }
    patchCtx.putImageData(new ImageData(new Uint8ClampedArray(f.patch), w, h), 0, 0);
    ctx.drawImage(patchCanvas, left, top);

    frames.push(await createImageBitmap(canvas));
    // Browsers treat delays under 20ms as 100ms; match that so stickers aren't hyperactive.
    elapsed += f.delay && f.delay >= 20 ? f.delay : 100;
    ends.push(elapsed);

    if (f.disposalType === 2) ctx.clearRect(left, top, w, h);
    else if (f.disposalType === 3 && previous) ctx.putImageData(previous, 0, 0);
  }

  if (!frames.length) throw new Error('GIF has no frames');
  return { width, height, frames, ends };
}

export function frameIndexAt(anim: DecodedAnimation, ms: number): number {
  const loop = anim.ends[anim.ends.length - 1];
  const t = ((ms % loop) + loop) % loop;
  let lo = 0;
  let hi = anim.ends.length - 1;
  while (lo < hi) {
    const m = (lo + hi) >> 1;
    if (anim.ends[m] > t) hi = m;
    else lo = m + 1;
  }
  return lo;
}
