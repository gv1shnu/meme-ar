import { describe, expect, it } from 'vitest';
import { trackPeople, iou } from './tracker';
import { detectEvents } from './detectors';
import { planReactions, DELAY_RANGE_MS } from './planner';
import { anchorAt, chooseLayout, placeAt } from './placement';
import { person, timeline } from './testkit';
import type { AudioEnvelope, EventType, SceneEvent } from './types';

const FPS = 8;
const types = (events: SceneEvent[]) => events.map((e) => e.type);
const detect = (frames: ReturnType<typeof timeline>, duration: number, audio: AudioEnvelope | null = null) =>
  detectEvents(trackPeople(frames), duration, audio);

describe('tracker', () => {
  it('keeps ids stable as people move and assigns new ids to newcomers', () => {
    const frames = timeline(3, FPS, (t) => [person({ cx: 0.3 + t * 0.02 }), ...(t >= 1.5 ? [person({ cx: 0.75 })] : [])]);
    const tracked = trackPeople(frames);
    expect(new Set(tracked.flatMap((f) => f.people.filter((p) => p.box.x < 0.5).map((p) => p.id)))).toEqual(new Set(['P1']));
    expect(tracked.at(-1)!.people.map((p) => p.id).sort()).toEqual(['P1', 'P2']);
  });

  it('bridges a short detection gap without a new id', () => {
    const frames = timeline(2, FPS, (t) => (t > 0.9 && t < 1.2 ? [] : [person()]));
    const ids = new Set(trackPeople(frames).flatMap((f) => f.people.map((p) => p.id)));
    expect([...ids]).toEqual(['P1']);
  });

  it('computes IoU', () => {
    expect(iou({ x: 0, y: 0, w: 1, h: 1 }, { x: 0, y: 0, w: 1, h: 1 })).toBe(1);
    expect(iou({ x: 0, y: 0, w: 1, h: 1 }, { x: 2, y: 2, w: 1, h: 1 })).toBe(0);
  });
});

describe('detectors', () => {
  it('finds nothing in a calm, silent-free clip of one still person', () => {
    expect(detect(timeline(4, FPS, () => [person()]), 4)).toEqual([]);
  });

  it('detects a celebration at its onset', () => {
    const events = detect(timeline(4, FPS, (t) => [person({ armsUp: t >= 1.5 && t < 2.5 })]), 4);
    const e = events.find((x) => x.type === 'celebrate');
    expect(e?.t).toBeCloseTo(1.5, 1);
    expect(e?.personId).toBe('P1');
  });

  it('detects a facepalm but not a brief hand flick', () => {
    expect(types(detect(timeline(4, FPS, (t) => [person({ handOnFace: t >= 1 && t < 2 })]), 4))).toContain('facepalm');
    expect(types(detect(timeline(4, FPS, (t) => [person({ handOnFace: t >= 1 && t < 1.2 })]), 4))).not.toContain('facepalm');
  });

  it('detects surprise only when the mouth drops open from closed', () => {
    const surprised = (t: number) => (t >= 2 ? { jawOpen: 0.7, eyeWide: 0.6 } : { jawOpen: 0.05 });
    const e = detect(timeline(4, FPS, (t) => [person({ face: surprised(t) })]), 4).find((x) => x.type === 'surprise');
    expect(e?.t).toBeCloseTo(2, 1);

    const alwaysOpen = detect(timeline(4, FPS, () => [person({ face: { jawOpen: 0.7, eyeWide: 0.6 } })]), 4);
    expect(types(alwaysOpen)).not.toContain('surprise');
  });

  it('detects a sustained laugh', () => {
    const events = detect(timeline(4, FPS, (t) => [person({ face: t >= 1 && t < 2.5 ? { smile: 0.8, jawOpen: 0.4 } : {} })]), 4);
    expect(types(events)).toContain('laugh');
  });

  it('detects a fall and prefers it over the accompanying sudden motion', () => {
    const hipY = (t: number) => (t < 2 ? 0.5 : t < 2.4 ? 0.5 + (t - 2) * 0.5 : 0.7);
    const tilt = (t: number) => (t < 2 ? 0 : Math.min(Math.PI / 2, (t - 2) * 4));
    const events = detect(timeline(4, FPS, (t) => [person({ hipY: hipY(t), tilt: tilt(t) })]), 4);
    expect(types(events)).toContain('fall');
    const plan = planReactions(events, 4);
    expect(plan[0].event.type).toBe('fall');
    expect(plan.some((r) => r.event.type === 'suddenMotion' && Math.abs(r.event.t - 2.25) < 1)).toBe(false);
  });

  it('does not call sitting down a fall', () => {
    const hipY = (t: number) => (t < 2 ? 0.5 : t < 2.4 ? 0.5 + (t - 2) * 0.5 : 0.7);
    expect(types(detect(timeline(4, FPS, (t) => [person({ hipY: hipY(t) })]), 4))).not.toContain('fall');
  });

  it('does not mistake a camera tilt for a fall or a jump', () => {
    // Person stands still; the camera tilts so they slide down the frame, then back up.
    const shift = (t: number) => (t < 2 ? 0 : t < 2.4 ? (t - 2) * 0.5 : t < 3 ? 0.2 : Math.max(0, 0.2 - (t - 3) * 0.5));
    const frames = timeline(5, FPS, (t) => [person({ hipY: 0.5 + shift(t), tilt: 0 })]);
    frames.forEach((f) => (f.camera = { x: 0, y: shift(f.t) }));
    const found = types(detectEvents(trackPeople(frames), 5));
    expect(found).not.toContain('fall');
    expect(found).not.toContain('jump');
  });

  it('detects a jump', () => {
    const hipY = (t: number) => (t >= 2 && t < 2.5 ? 0.5 : 0.6);
    expect(types(detect(timeline(4, FPS, (t) => [person({ hipY: hipY(t) })]), 4))).toContain('jump');
  });

  it('detects someone reaching for food', () => {
    const pizza = { label: 'pizza', score: 0.9, box: { x: 0.62, y: 0.55, w: 0.1, h: 0.08 } };
    // Person slides right so their wrist lands on the pizza at ~t=2.
    const frames = timeline(4, FPS, (t) => [person({ cx: t < 2 ? 0.4 : 0.6 })], () => [pizza]);
    expect(types(detectEvents(trackPeople(frames), 4))).toContain('snack');
  });

  it('detects a pet that sticks around, but not a one-frame false positive', () => {
    const dog = { label: 'dog', score: 0.8, box: { x: 0.1, y: 0.6, w: 0.2, h: 0.2 } };
    expect(types(detect(timeline(4, FPS, () => [], (t) => (t >= 1 ? [dog] : [])), 4))).toContain('pet');
    expect(types(detect(timeline(4, FPS, () => [], (t) => (t === 1 ? [dog] : [])), 4))).not.toContain('pet');
  });

  it('detects someone walking in and out through the side of the frame', () => {
    // P2 walks in from the right edge at t=2, stands, then walks back out by t=6.
    const cx = (t: number) => (t < 3 ? 1.02 - (t - 2) * 0.25 : t < 5 ? 0.77 : 0.77 + (t - 5) * 0.25);
    const events = detect(timeline(8, FPS, (t) => [person({ cx: 0.3 }), ...(t >= 2 && t < 6 ? [person({ cx: cx(t) })] : [])]), 8);
    expect(events.filter((e) => e.type === 'enter').map((e) => e.personId)).toEqual(['P2']);
    expect(events.filter((e) => e.type === 'leave').map((e) => e.personId)).toEqual(['P2']);
  });

  it('ignores people who pop in mid-frame (detector flicker)', () => {
    const events = detect(timeline(8, FPS, (t) => [person({ cx: 0.3 }), ...(t >= 2 && t < 5 ? [person({ cx: 0.7 })] : [])]), 8);
    expect(types(events)).not.toContain('enter');
    expect(types(events)).not.toContain('leave');
  });

  it('treats a camera cut as a new shot, not as people leaving, entering or falling', () => {
    // Before the cut: one person standing high in frame. After: someone else lower, at the edge.
    const frames = timeline(8, FPS, (t) => [t < 4 ? person({ cx: 0.5, hipY: 0.45 }) : person({ cx: 0.9, hipY: 0.75 })]);
    frames.forEach((f) => (f.cut = f.t === 4));
    const tracked = trackPeople(frames);
    expect(tracked.find((f) => f.t === 4)!.people[0].id).toBe('P2');
    expect(detectEvents(tracked, 8)).toEqual([]);
  });

  it('detects sustained flailing as sudden motion', () => {
    const flail = (t: number) => t >= 2 && t < 2.6 && Math.round(t * FPS) % 2 === 0;
    expect(types(detect(timeline(4, FPS, (t) => [person({ armsUp: flail(t) })]), 4))).toContain('suddenMotion');
  });

  it('does not mistake a camera pan for sudden motion', () => {
    // The whole person sweeps across the frame quickly, as in a pan.
    const events = detect(timeline(4, FPS, (t) => [person({ cx: 0.2 + t * 0.15 })]), 4);
    expect(types(events)).not.toContain('suddenMotion');
  });

  it('ignores jittery, implausible poses (pose model guessing in a crowd)', () => {
    // Torso length and orientation flip every frame: no real body does that.
    const events = detect(timeline(4, FPS, (t) => [person({ torso: Math.round(t * FPS) % 2 ? 0.25 : 0.14, tilt: Math.round(t * FPS) % 2 ? 1.2 : 0 })]), 4);
    expect(types(events)).not.toContain('suddenMotion');
    expect(types(events)).not.toContain('fall');
  });

  it('ignores tiny background people', () => {
    const events = detect(timeline(4, FPS, (t) => [person({ torso: 0.03, armsUp: t >= 1 && t < 2 })]), 4);
    expect(events).toEqual([]);
  });

  it('detects a loud spike and an awkward quiet stretch from audio', () => {
    const hz = 20;
    const rms = new Float32Array(10 * hz).map((_, i) => (i / hz < 5 ? 0.1 : 0.005));
    rms[3 * hz] = 0.6;
    const events = detect(timeline(10, FPS, () => [person()]), 10, { hz, rms });
    const loud = events.find((e) => e.type === 'loud');
    expect(loud?.t).toBeCloseTo(3, 1);
    const awkward = events.find((e) => e.type === 'awkward');
    expect(awkward && awkward.t).toBeGreaterThanOrEqual(8);
  });
});

describe('planner', () => {
  const ev = (type: EventType, t: number, confidence = 0.9, personId = 'P1'): SceneEvent => ({ type, t, confidence, personId });

  it('applies comedic delay from each event type timing mode', () => {
    const plan = planReactions([ev('surprise', 1), ev('fall', 5)], 10, { seed: 3 });
    const [surprise, fall] = plan;
    expect(surprise.timing).toBe('instant');
    expect(surprise.delayMs).toBeLessThanOrEqual(DELAY_RANGE_MS.instant[1]);
    expect(fall.timing).toBe('delayed');
    expect(fall.delayMs).toBeGreaterThanOrEqual(DELAY_RANGE_MS.delayed[0]);
    expect(fall.start).toBeCloseTo(5 + fall.delayMs / 1000, 5);
  });

  it('is deterministic for a given seed', () => {
    const events = [ev('laugh', 1), ev('celebrate', 4), ev('pet', 8, 0.8, undefined)];
    expect(planReactions(events, 12, { seed: 7 })).toEqual(planReactions(events, 12, { seed: 7 }));
  });

  it('keeps reactions spaced out and prefers the funnier event', () => {
    const plan = planReactions([ev('enter', 2, 0.9, 'P2'), ev('fall', 2.3)], 10);
    expect(plan).toHaveLength(1);
    expect(plan[0].event.type).toBe('fall');
  });

  it('enforces a per-type cooldown and an overall density budget', () => {
    const laughs = [0, 2, 4, 6, 8].map((t) => ev('laugh', t, 0.9, `P${t}`));
    expect(planReactions(laughs, 20).length).toBe(2);

    const many = (['surprise', 'laugh', 'celebrate', 'facepalm', 'jump', 'snack', 'fall', 'pet'] as EventType[]).map((type, i) => ev(type, i * 2, 0.9, `P${i}`));
    expect(planReactions(many, 8, { secondsPerReaction: 4 }).length).toBeLessThanOrEqual(2);
  });

  it('drops weak events and ones that would start after the video ends', () => {
    expect(planReactions([ev('suddenMotion', 1, 0.2)], 10)).toEqual([]);
    expect(planReactions([ev('fall', 9.9)], 10)).toEqual([]);
  });
});

describe('placement', () => {
  const frames = trackPeople(timeline(3, FPS, (t) => [person({ cx: 0.3 + 0.1 * t })]));
  const reaction = planReactions([{ type: 'celebrate', t: 1, confidence: 0.9, personId: 'P1' }], 3)[0];

  it('interpolates the head between samples', () => {
    const a = anchorAt(frames, 'P1', 1.0)!;
    const b = anchorAt(frames, 'P1', 1.0625)!;
    expect(b.head.x - a.head.x).toBeCloseTo(0.00625, 4);
  });

  it('puts person stickers on the roomier side and follows the person', () => {
    const layout = chooseLayout(frames, reaction);
    expect(layout).toMatchObject({ mode: 'person', side: 1 });
    const r1 = placeAt(frames, reaction, layout, reaction.start, 1920, 1080, 1);
    const r2 = placeAt(frames, reaction, layout, reaction.start + 1, 1920, 1080, 1);
    expect(r2.x).toBeGreaterThan(r1.x);
  });

  it('keeps stickers inside the frame', () => {
    const edge = trackPeople(timeline(2, FPS, () => [person({ cx: 0.97 })]));
    const r = planReactions([{ type: 'laugh', t: 0.5, confidence: 0.9, personId: 'P1' }], 2)[0];
    const rect = placeAt(edge, r, chooseLayout(edge, r), r.start, 1280, 720, 1.5);
    expect(rect.x).toBeGreaterThanOrEqual(0);
    expect(rect.x + rect.w).toBeLessThanOrEqual(1280);
    expect(rect.y).toBeGreaterThanOrEqual(0);
  });

  it('sends scene-level reactions to the emptiest corner', () => {
    const left = trackPeople(timeline(4, FPS, () => [person({ cx: 0.2, hipY: 0.4 })]));
    const r = planReactions([{ type: 'awkward', t: 1, confidence: 0.9 }], 4)[0];
    const layout = chooseLayout(left, r);
    expect(layout.mode).toBe('corner');
    expect(layout.corner % 2).toBe(1); // a right-hand corner
  });
});
