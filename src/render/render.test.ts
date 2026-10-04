import { describe, expect, it } from 'vitest';
import { animationAt } from './compositor';
import { pickMimeType } from './exporter';
import { frameIndexAt, type DecodedAnimation } from '../stickers/gif';

describe('sticker animation', () => {
  it('pops in, holds, and fades out', () => {
    expect(animationAt(0, 2.4, 'beat').alpha).toBe(0);
    const hold = animationAt(1.2, 2.4, 'beat');
    expect(hold.alpha).toBe(1);
    expect(hold.scale).toBeCloseTo(1, 1);
    expect(animationAt(2.4, 2.4, 'beat').alpha).toBe(0);
  });
});

describe('gif frame timing', () => {
  const anim = { width: 1, height: 1, frames: [], ends: [100, 200, 400] } as unknown as DecodedAnimation;
  it('maps time to frames and loops', () => {
    expect(frameIndexAt(anim, 0)).toBe(0);
    expect(frameIndexAt(anim, 150)).toBe(1);
    expect(frameIndexAt(anim, 399)).toBe(2);
    expect(frameIndexAt(anim, 450)).toBe(0);
  });
});

describe('export format', () => {
  it('prefers MP4 and falls back to WebM', () => {
    expect(pickMimeType(() => true)).toMatch(/^video\/mp4/);
    expect(pickMimeType((m) => m.startsWith('video/webm'))).toMatch(/^video\/webm/);
    expect(pickMimeType(() => false)).toBeNull();
  });
});
