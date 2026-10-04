import { LM, type Box, type PlannedReaction, type Point, type TrackedFrame, type TrackedPerson } from './types';

/**
 * Decides where a sticker sits, in video pixels, at any moment of its lifetime.
 * Person reactions ride next to that person's head and follow them; scene-level
 * reactions (awkward silence, loud noise, pets) take the emptiest corner.
 * The side/corner is fixed when the reaction starts so stickers never flip
 * mid-animation.
 */

export interface Rect {
  x: number;
  y: number;
  w: number;
  h: number;
}

interface Anchor {
  head: Point;
  /** Person height, normalized. */
  height: number;
}

export interface Layout {
  mode: 'person' | 'corner';
  side: -1 | 1;
  corner: 0 | 1 | 2 | 3; // TL, TR, BL, BR
}

function headOf(p: TrackedPerson): Point {
  const n = p.landmarks[LM.nose];
  if (n && n.v >= 0.5) return { x: n.x, y: n.y };
  return { x: p.box.x + p.box.w / 2, y: p.box.y + p.box.h * 0.12 };
}

/** Person's head position at time t, linearly interpolated between samples. */
export function anchorAt(frames: TrackedFrame[], personId: string, t: number): Anchor | null {
  let before: { t: number; p: TrackedPerson } | null = null;
  let after: { t: number; p: TrackedPerson } | null = null;
  for (const f of frames) {
    const p = f.people.find((x) => x.id === personId);
    if (!p) continue;
    if (f.t <= t) before = { t: f.t, p };
    else {
      after = { t: f.t, p };
      break;
    }
  }
  const a = before ?? after;
  if (!a) return null;
  const b = after ?? before!;
  const k = b.t === a.t ? 0 : (t - a.t) / (b.t - a.t);
  const ha = headOf(a.p), hb = headOf(b.p);
  return {
    head: { x: ha.x + (hb.x - ha.x) * k, y: ha.y + (hb.y - ha.y) * k },
    height: a.p.box.h + (b.p.box.h - a.p.box.h) * k,
  };
}

function frameAt(frames: TrackedFrame[], t: number): TrackedFrame | undefined {
  let best: TrackedFrame | undefined;
  for (const f of frames) {
    if (!best || Math.abs(f.t - t) < Math.abs(best.t - t)) best = f;
  }
  return best;
}

function overlap(a: Box, b: Box): number {
  const w = Math.max(0, Math.min(a.x + a.w, b.x + b.w) - Math.max(a.x, b.x));
  const h = Math.max(0, Math.min(a.y + a.h, b.y + b.h) - Math.max(a.y, b.y));
  return w * h;
}

export function chooseLayout(frames: TrackedFrame[], r: PlannedReaction): Layout {
  const f = frameAt(frames, r.start);
  const people = f?.people ?? [];
  const id = r.event.personId;
  const anchor = id ? anchorAt(frames, id, r.start) : null;

  if (anchor) {
    // Prefer the side with more room and fewer other faces.
    const others = people.filter((p) => p.id !== id).map(headOf);
    const room = (side: -1 | 1) => {
      const space = side < 0 ? anchor.head.x : 1 - anchor.head.x;
      const crowd = others.filter((h) => Math.sign(h.x - anchor.head.x) === side && Math.abs(h.x - anchor.head.x) < 0.3).length;
      return space - 0.25 * crowd;
    };
    return { mode: 'person', side: room(1) >= room(-1) ? 1 : -1, corner: 1 };
  }

  const corners: Box[] = [
    { x: 0, y: 0, w: 0.4, h: 0.4 },
    { x: 0.6, y: 0, w: 0.4, h: 0.4 },
    { x: 0, y: 0.6, w: 0.4, h: 0.4 },
    { x: 0.6, y: 0.6, w: 0.4, h: 0.4 },
  ];
  let best = 1;
  let bestCost = Infinity;
  corners.forEach((c, i) => {
    // Small bias toward top corners: they read as "caption" positions.
    const cost = people.reduce((acc, p) => acc + overlap(c, p.box), 0) + (i >= 2 ? 0.02 : 0);
    if (cost < bestCost) {
      bestCost = cost;
      best = i;
    }
  });
  return { mode: 'corner', side: 1, corner: best as Layout['corner'] };
}

/**
 * Sticker rect in video pixels at time t. `aspect` is sticker width / height.
 */
export function placeAt(
  frames: TrackedFrame[],
  r: PlannedReaction,
  layout: Layout,
  t: number,
  videoW: number,
  videoH: number,
  aspect: number,
): Rect {
  const minDim = Math.min(videoW, videoH);
  const margin = minDim * 0.03;
  const clampRect = (x: number, y: number, w: number, h: number): Rect => ({
    x: Math.max(margin, Math.min(videoW - w - margin, x)),
    y: Math.max(margin, Math.min(videoH - h - margin, y)),
    w,
    h,
  });

  const anchor = layout.mode === 'person' && r.event.personId ? anchorAt(frames, r.event.personId, t) : null;
  if (anchor) {
    const h = Math.max(minDim * 0.26, Math.min(minDim * 0.45, anchor.height * videoH * 0.5));
    const w = h * aspect;
    const hx = anchor.head.x * videoW;
    const hy = anchor.head.y * videoH;
    const gap = h * 0.25;
    const x = layout.side > 0 ? hx + gap : hx - gap - w;
    const y = hy - h * 0.85;
    return clampRect(x, y, w, h);
  }

  const h = minDim * 0.36;
  const w = h * aspect;
  const left = layout.corner % 2 === 0;
  const top = layout.corner < 2;
  return clampRect(left ? margin : videoW - w - margin, top ? margin : videoH - h - margin, w, h);
}
