import type { Box, FrameObservation, PersonObservation, TrackedFrame, TrackedPerson } from './types';

/**
 * Greedy IoU + center-distance tracker. Assigns ephemeral ids (P1, P2, ...) that
 * stay stable while a person remains in view. A track survives short detection
 * gaps (`maxGapSeconds`) so a missed frame does not look like leave + re-enter.
 * No identity recognition: ids never persist beyond one video.
 */
export interface TrackerOptions {
  maxGapSeconds: number;
  /** Minimum match score (0..1) required to continue a track. */
  minMatch: number;
}

const DEFAULTS: TrackerOptions = { maxGapSeconds: 0.8, minMatch: 0.15 };

interface Track {
  id: string;
  box: Box;
  lastSeen: number;
}

export function iou(a: Box, b: Box): number {
  const x1 = Math.max(a.x, b.x);
  const y1 = Math.max(a.y, b.y);
  const x2 = Math.min(a.x + a.w, b.x + b.w);
  const y2 = Math.min(a.y + a.h, b.y + b.h);
  const inter = Math.max(0, x2 - x1) * Math.max(0, y2 - y1);
  const union = a.w * a.h + b.w * b.h - inter;
  return union > 0 ? inter / union : 0;
}

function matchScore(a: Box, b: Box): number {
  const ca = { x: a.x + a.w / 2, y: a.y + a.h / 2 };
  const cb = { x: b.x + b.w / 2, y: b.y + b.h / 2 };
  const scale = Math.max(a.h, b.h, 0.05);
  const dist = Math.hypot(ca.x - cb.x, ca.y - cb.y) / scale;
  const proximity = Math.max(0, 1 - dist);
  return 0.6 * iou(a, b) + 0.4 * proximity;
}

export function trackPeople(frames: FrameObservation[], options: Partial<TrackerOptions> = {}): TrackedFrame[] {
  const opts = { ...DEFAULTS, ...options };
  let tracks: Track[] = [];
  let nextId = 1;
  const out: TrackedFrame[] = [];

  for (const frame of frames) {
    // Nothing carries across a camera cut: the same position is a different person.
    tracks = frame.cut ? [] : tracks.filter((tr) => frame.t - tr.lastSeen <= opts.maxGapSeconds);

    const pairs: { ti: number; pi: number; s: number }[] = [];
    tracks.forEach((tr, ti) =>
      frame.people.forEach((p, pi) => {
        const s = matchScore(tr.box, p.box);
        if (s >= opts.minMatch) pairs.push({ ti, pi, s });
      }),
    );
    pairs.sort((a, b) => b.s - a.s);

    const usedTracks = new Set<number>();
    const assigned = new Map<number, string>();
    for (const { ti, pi } of pairs) {
      if (usedTracks.has(ti) || assigned.has(pi)) continue;
      usedTracks.add(ti);
      assigned.set(pi, tracks[ti].id);
    }

    const people: TrackedPerson[] = frame.people.map((p: PersonObservation, pi) => {
      let id = assigned.get(pi);
      if (!id) {
        id = `P${nextId++}`;
        tracks.push({ id, box: p.box, lastSeen: frame.t });
      }
      const tr = tracks.find((x) => x.id === id)!;
      tr.box = p.box;
      tr.lastSeen = frame.t;
      return { ...p, id };
    });

    out.push({ t: frame.t, people, objects: frame.objects, cut: frame.cut, camera: frame.camera });
  }
  return out;
}
