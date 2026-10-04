import type { Point } from './types';

/**
 * Camera analysis from tiny thumbnails of consecutive samples:
 *  - cut detection, so tracking and events never span two shots;
 *  - global shift estimation, so a pan or tilt is not mistaken for people
 *    moving (e.g. hips "dropping" as the camera tilts up).
 */

export interface Thumb {
  /** Luma at SHIFT_W x SHIFT_H, for shift estimation. */
  luma: Float32Array;
  /** RGB at CUT_W x CUT_H, for cut detection. */
  rgb: Uint8ClampedArray;
  /** 64-bin (4x4x4) color histogram. */
  hist: Float32Array;
}

export const SHIFT_W = 64;
export const SHIFT_H = 36;
export const CUT_W = 32;
export const CUT_H = 18;

/**
 * A cut changes both the pixels and the color distribution; a pan or zoom
 * changes pixels but keeps the color mix. Thresholds tuned on real footage
 * (hard cuts scored >= 0.19 / 0.25, fast pans <= 0.165 / 0.11).
 */
const CUT_PIXEL_DIFF = 0.17;
const CUT_HIST_DIFF = 0.2;

export function makeThumb(shiftRGBA: Uint8ClampedArray, cutRGBA: Uint8ClampedArray): Thumb {
  const luma = new Float32Array(SHIFT_W * SHIFT_H);
  for (let i = 0; i < luma.length; i++) {
    luma[i] = (0.299 * shiftRGBA[i * 4] + 0.587 * shiftRGBA[i * 4 + 1] + 0.114 * shiftRGBA[i * 4 + 2]) / 255;
  }
  const n = CUT_W * CUT_H;
  const rgb = new Uint8ClampedArray(n * 3);
  const hist = new Float32Array(64);
  for (let i = 0; i < n; i++) {
    const r = cutRGBA[i * 4], g = cutRGBA[i * 4 + 1], b = cutRGBA[i * 4 + 2];
    rgb[i * 3] = r;
    rgb[i * 3 + 1] = g;
    rgb[i * 3 + 2] = b;
    hist[(r >> 6) * 16 + (g >> 6) * 4 + (b >> 6)] += 1 / n;
  }
  return { luma, rgb, hist };
}

export function isCut(a: Thumb, b: Thumb): boolean {
  let pix = 0;
  for (let i = 0; i < a.rgb.length; i++) pix += Math.abs(a.rgb[i] - b.rgb[i]);
  pix /= a.rgb.length * 255;
  let hist = 0;
  for (let i = 0; i < 64; i++) hist += Math.abs(a.hist[i] - b.hist[i]);
  hist /= 2;
  return pix > CUT_PIXEL_DIFF && hist > CUT_HIST_DIFF;
}

/**
 * How far the image content moved from `a` to `b` (normalized frame units),
 * by exhaustive block matching over +-maxShift thumbnail pixels.
 * Content moving down (camera tilting up) gives a positive y.
 */
export function estimateShift(a: Float32Array, b: Float32Array, maxShift = 6, w = SHIFT_W, h = SHIFT_H): Point {
  let best = { dx: 0, dy: 0 };
  let bestCost = Infinity;
  for (let dy = -maxShift; dy <= maxShift; dy++) {
    for (let dx = -maxShift; dx <= maxShift; dx++) {
      let sum = 0;
      let count = 0;
      for (let y = Math.max(0, -dy); y < Math.min(h, h - dy); y++) {
        for (let x = Math.max(0, -dx); x < Math.min(w, w - dx); x++) {
          sum += Math.abs(a[y * w + x] - b[(y + dy) * w + (x + dx)]);
          count++;
        }
      }
      // Tiny preference for "no motion" breaks ties on flat, featureless frames.
      const cost = sum / count + 1e-4 * (Math.abs(dx) + Math.abs(dy));
      if (cost < bestCost) {
        bestCost = cost;
        best = { dx, dy };
      }
    }
  }
  return { x: best.dx / w, y: best.dy / h };
}
