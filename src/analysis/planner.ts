import type { EventType, PlannedReaction, SceneEvent, TimingMode } from './types';
import { rng } from '../util/random';

/**
 * Turns detected events into a schedule of reactions. Two ideas carry over from
 * the original engine:
 *
 *  1. Not every event deserves a meme. Events are scored (how inherently funny
 *     the event type is x detector confidence), then greedily accepted subject to
 *     spacing, per-type cooldown and an overall density cap so the video never
 *     turns into sticker spam.
 *  2. Comedic delay is a deliberate creative choice, separate from compute. Each
 *     event type maps to a timing mode, and the actual delay is sampled (seeded,
 *     so a plan is reproducible) from that mode's range.
 */

export interface PlannerOptions {
  seed: number;
  /** Minimum seconds between two reactions starting. */
  minGap: number;
  /** Seconds before the same event type may fire again. */
  typeCooldown: number;
  /** At most one reaction per this many seconds of video, on average. */
  secondsPerReaction: number;
  minScore: number;
  maxConcurrent: number;
}

export const DEFAULT_PLANNER: PlannerOptions = {
  seed: 1,
  minGap: 1.8,
  typeCooldown: 6,
  secondsPerReaction: 4,
  minScore: 0.3,
  maxConcurrent: 2,
};

/** How funny each event type tends to be on its own. */
export const EVENT_WEIGHT: Record<EventType, number> = {
  fall: 1,
  surprise: 0.9,
  facepalm: 0.85,
  snack: 0.8,
  pet: 0.75,
  celebrate: 0.75,
  awkward: 0.7,
  jump: 0.7,
  laugh: 0.7,
  loud: 0.65,
  phone: 0.55,
  leave: 0.5,
  enter: 0.45,
  suddenMotion: 0.4,
};

const TIMING: Record<EventType, TimingMode> = {
  surprise: 'instant',
  loud: 'instant',
  suddenMotion: 'instant',
  jump: 'instant',
  celebrate: 'beat',
  facepalm: 'beat',
  snack: 'beat',
  laugh: 'beat',
  pet: 'beat',
  enter: 'beat',
  phone: 'beat',
  fall: 'delayed',
  awkward: 'delayed',
  leave: 'delayed',
};

export const DELAY_RANGE_MS: Record<TimingMode, [number, number]> = {
  instant: [0, 80],
  beat: [150, 400],
  delayed: [450, 900],
};

const DURATION: Partial<Record<EventType, number>> = { awkward: 3, leave: 2, enter: 2.2, suddenMotion: 2 };

export function scoreEvent(e: SceneEvent): number {
  return EVENT_WEIGHT[e.type] * e.confidence + 0.15 * (e.magnitude ?? 0);
}

export function planReactions(events: SceneEvent[], videoDuration: number, options: Partial<PlannerOptions> = {}): PlannedReaction[] {
  const opts = { ...DEFAULT_PLANNER, ...options };
  const random = rng(opts.seed);
  const budget = Math.max(1, Math.ceil(videoDuration / opts.secondsPerReaction));

  // Same person, same moment: keep only the most specific/funny reading
  // (a fall also registers as sudden motion; we want the fall).
  const ranked = [...events].sort((a, b) => scoreEvent(b) - scoreEvent(a));
  const distinct: SceneEvent[] = [];
  for (const e of ranked) {
    if (!distinct.some((d) => d.personId && d.personId === e.personId && Math.abs(d.t - e.t) < 1)) distinct.push(e);
  }

  const accepted: PlannedReaction[] = [];
  for (const e of distinct) {
    if (accepted.length >= budget) break;
    const score = scoreEvent(e);
    if (score < opts.minScore) continue;

    const timing = TIMING[e.type];
    const [lo, hi] = DELAY_RANGE_MS[timing];
    const delayMs = Math.round(lo + random() * (hi - lo));
    const start = e.t + delayMs / 1000;
    const duration = Math.min(DURATION[e.type] ?? 2.4, Math.max(0.8, videoDuration - start));
    if (start >= videoDuration - 0.3) continue;

    if (accepted.some((a) => Math.abs(a.start - start) < opts.minGap)) continue;
    if (accepted.some((a) => a.event.type === e.type && Math.abs(a.start - start) < opts.typeCooldown)) continue;
    const concurrent = accepted.filter((a) => a.start < start + duration && start < a.start + a.duration).length;
    if (concurrent >= opts.maxConcurrent) continue;

    accepted.push({ id: `r${accepted.length + 1}-${e.type}`, event: e, start, duration, timing, delayMs, score });
  }
  return accepted.sort((a, b) => a.start - b.start);
}
