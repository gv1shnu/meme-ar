import { describe, expect, it } from 'vitest';
import { CUT_H, CUT_W, SHIFT_H, SHIFT_W, estimateShift, isCut, makeThumb } from './camera';

/** A textured test image: smooth blobs so block matching has something to lock on to. */
function scene(w: number, h: number, ox = 0, oy = 0, hue = 0): Uint8ClampedArray {
  const d = new Uint8ClampedArray(w * h * 4);
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const u = (x - ox) / w, v = (y - oy) / h;
      const val = 128 + 60 * Math.sin(u * 17 + hue) + 50 * Math.cos(v * 13 - u * 5);
      const i = (y * w + x) * 4;
      d[i] = val;
      d[i + 1] = (val + hue * 80) % 256;
      d[i + 2] = 255 - val;
      d[i + 3] = 255;
    }
  }
  return d;
}

describe('camera', () => {
  it('measures how far the picture shifted', () => {
    const a = makeThumb(scene(SHIFT_W, SHIFT_H), scene(CUT_W, CUT_H));
    const b = makeThumb(scene(SHIFT_W, SHIFT_H, 3, -2), scene(CUT_W, CUT_H));
    const d = estimateShift(a.luma, b.luma);
    expect(d.x).toBeCloseTo(3 / SHIFT_W, 5);
    expect(d.y).toBeCloseTo(-2 / SHIFT_H, 5);
  });

  it('reports no shift for identical frames', () => {
    const a = makeThumb(scene(SHIFT_W, SHIFT_H), scene(CUT_W, CUT_H));
    expect(estimateShift(a.luma, a.luma)).toEqual({ x: 0, y: 0 });
  });

  it('flags a cut to different content but not a pan of the same scene', () => {
    const a = makeThumb(scene(SHIFT_W, SHIFT_H), scene(CUT_W, CUT_H));
    const panned = makeThumb(scene(SHIFT_W, SHIFT_H, 4, 0), scene(CUT_W, CUT_H, 2, 0));
    const other = makeThumb(scene(SHIFT_W, SHIFT_H, 0, 0, 2.5), scene(CUT_W, CUT_H, 0, 0, 2.5));
    expect(isCut(a, panned)).toBe(false);
    expect(isCut(a, other)).toBe(true);
  });
});
