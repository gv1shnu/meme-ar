import { LM, type AudioEnvelope, type Box, type EventType, type Landmark, type Point, type SceneEvent, type TrackedFrame, type TrackedPerson } from './types';

/**
 * Offline event detection over a fully tracked video. Because the whole clip is
 * known up front, detectors can confirm that a cue is *sustained* and still emit
 * the event at its true onset — no live prediction needed.
 *
 * Distances are normalized by the person's torso length so thresholds hold
 * regardless of how near the person stands to the camera.
 */

interface Sample {
  t: number;
  p: TrackedPerson;
  /** Camera offset at this sample, for stabilized positions. */
  cam: Point;
}

const FOOD = new Set(['banana', 'apple', 'sandwich', 'orange', 'broccoli', 'carrot', 'hot dog', 'pizza', 'donut', 'cake']);
const PETS = new Set(['dog', 'cat']);

const vis = (l: Landmark | undefined): l is Landmark => !!l && l.v >= 0.5;
const dist = (a: Point, b: Point) => Math.hypot(a.x - b.x, a.y - b.y);
const mid = (a: Point, b: Point): Point => ({ x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 });
const clamp01 = (v: number) => Math.max(0, Math.min(1, v));
const center = (b: Box): Point => ({ x: b.x + b.w / 2, y: b.y + b.h / 2 });

/** Torso length (shoulder midpoint to hip midpoint), falling back to box height. */
export function bodyScale(p: TrackedPerson): number {
  const L = p.landmarks;
  const ls = L[LM.leftShoulder], rs = L[LM.rightShoulder], lh = L[LM.leftHip], rh = L[LM.rightHip];
  if (vis(ls) && vis(rs) && vis(lh) && vis(rh)) {
    return Math.max(0.03, dist(mid(ls, rs), mid(lh, rh)));
  }
  return Math.max(0.03, p.box.h * 0.35);
}

function hipCenter(p: TrackedPerson): Point | null {
  const lh = p.landmarks[LM.leftHip], rh = p.landmarks[LM.rightHip];
  return vis(lh) && vis(rh) ? mid(lh, rh) : null;
}

/** Hip center with camera motion removed, so pans and tilts don't look like falls or jumps. */
function stableHip(s: Sample): Point | null {
  const h = hipCenter(s.p);
  return h ? { x: h.x - s.cam.x, y: h.y - s.cam.y } : null;
}

/** Shoulder-midpoint to hip-midpoint angle from vertical (radians), or null. */
function torsoAngle(p: TrackedPerson): number | null {
  const L = p.landmarks;
  const ls = L[LM.leftShoulder], rs = L[LM.rightShoulder];
  const hip = hipCenter(p);
  if (!hip || !vis(ls) || !vis(rs)) return null;
  const sh = mid(ls, rs);
  return Math.atan2(sh.x - hip.x, hip.y - sh.y);
}

/**
 * Real bodies can't change torso length or orientation much in one sample
 * interval. When they "do", the pose model is guessing (crowds, partial bodies
 * at the frame edge), and motion between those samples is noise.
 */
function consistent(a: TrackedPerson, b: TrackedPerson): boolean {
  const aa = torsoAngle(a), ab = torsoAngle(b);
  if (aa === null || ab === null) return false;
  const ratio = bodyScale(b) / bodyScale(a);
  const turn = Math.abs(Math.atan2(Math.sin(ab - aa), Math.cos(ab - aa)));
  return ratio > 0.7 && ratio < 1.43 && turn < 0.9;
}

const feetVisible = (p: TrackedPerson) => vis(p.landmarks[LM.leftAnkle]) && vis(p.landmarks[LM.rightAnkle]);

/** True when the body has toppled: torso far from upright, or head down near hip level. */
function toppled(p: TrackedPerson): boolean {
  const L = p.landmarks;
  const ls = L[LM.leftShoulder], rs = L[LM.rightShoulder], n = L[LM.nose];
  const hip = hipCenter(p);
  if (!hip || !vis(ls) || !vis(rs)) return false;
  const sh = mid(ls, rs);
  const tilt = Math.atan2(Math.abs(sh.x - hip.x), Math.abs(sh.y - hip.y));
  const headDown = vis(n) && n.y > hip.y - 0.25 * bodyScale(p);
  return tilt > Math.PI / 4 || headDown;
}

/** People smaller than this (fraction of frame height) are background: too small to read or react to. */
export const MIN_PERSON_HEIGHT = 0.15;

function seriesByPerson(frames: TrackedFrame[]): Map<string, Sample[]> {
  const map = new Map<string, Sample[]>();
  for (const f of frames) {
    for (const p of f.people) {
      if (p.box.h < MIN_PERSON_HEIGHT) continue;
      let s = map.get(p.id);
      if (!s) map.set(p.id, (s = []));
      s.push({ t: f.t, p, cam: f.camera ?? { x: 0, y: 0 } });
    }
  }
  return map;
}

/**
 * Finds runs where `pred` holds continuously for at least `minDur` seconds and
 * returns each run's onset time plus the mean of `strength` over the run.
 */
function sustainedRuns(
  samples: Sample[],
  pred: (s: Sample) => boolean,
  minDur: number,
  strength: (s: Sample) => number,
): { t: number; strength: number }[] {
  const out: { t: number; strength: number }[] = [];
  let start = -1;
  const flush = (endIdx: number) => {
    if (start < 0) return;
    const run = samples.slice(start, endIdx);
    if (run.length && run[run.length - 1].t - run[0].t >= minDur) {
      out.push({ t: run[0].t, strength: run.reduce((a, s) => a + strength(s), 0) / run.length });
    }
    start = -1;
  };
  samples.forEach((s, i) => {
    if (pred(s)) {
      if (start < 0) start = i;
    } else {
      flush(i);
    }
  });
  flush(samples.length);
  return out;
}

/** Body anchor (hip midpoint, else shoulder midpoint) that limb motion is measured against. */
function bodyAnchor(p: TrackedPerson): Point | null {
  const L = p.landmarks;
  const ls = L[LM.leftShoulder], rs = L[LM.rightShoulder];
  return hipCenter(p) ?? (vis(ls) && vis(rs) ? mid(ls, rs) : null);
}

/**
 * Mean per-second speed of the head and hands *relative to the body*, in torso
 * lengths. Measuring against the body cancels camera pans and zooms, which move
 * everything in frame together and would otherwise read as frantic motion.
 */
function motion(a: Sample, b: Sample): number {
  const dt = b.t - a.t;
  const ba = bodyAnchor(a.p), bb = bodyAnchor(b.p);
  if (dt <= 0 || !ba || !bb || !consistent(a.p, b.p)) return 0;
  const keys = [LM.nose, LM.leftWrist, LM.rightWrist, LM.leftShoulder, LM.rightShoulder];
  let sum = 0;
  let n = 0;
  for (const k of keys) {
    const la = a.p.landmarks[k], lb = b.p.landmarks[k];
    if (vis(la) && vis(lb)) {
      sum += Math.hypot(lb.x - bb.x - (la.x - ba.x), lb.y - bb.y - (la.y - ba.y));
      n++;
    }
  }
  if (n < 3) return 0;
  return sum / n / dt / bodyScale(b.p);
}

function detectFace(id: string, s: Sample[]): SceneEvent[] {
  const out: SceneEvent[] = [];
  const faced = s.filter((x) => x.p.face);

  // Surprise: jaw drops open with wide eyes / raised brows, from a closed mouth.
  for (let i = 0; i < faced.length; i++) {
    const f = faced[i].p.face!;
    if (f.jawOpen < 0.45 || Math.max(f.eyeWide, f.browUp) < 0.3) continue;
    const before = faced.filter((x) => x.t < faced[i].t && x.t >= faced[i].t - 0.8);
    if (!before.length || Math.min(...before.map((x) => x.p.face!.jawOpen)) > 0.2) continue;
    out.push({ type: 'surprise', t: faced[i].t, personId: id, confidence: clamp01(0.5 * f.jawOpen + 0.7 * Math.max(f.eyeWide, f.browUp)) });
  }

  // Laugh: a big open smile held for a moment.
  for (const r of sustainedRuns(faced, (x) => x.p.face!.smile >= 0.55 && x.p.face!.jawOpen >= 0.15, 0.6, (x) => x.p.face!.smile)) {
    out.push({ type: 'laugh', t: r.t, personId: id, confidence: clamp01(r.strength) });
  }
  return out;
}

function detectPose(id: string, s: Sample[]): SceneEvent[] {
  const out: SceneEvent[] = [];

  // Celebrate: both wrists above the head.
  const armsUp = (x: Sample) => {
    const L = x.p.landmarks;
    const n = L[LM.nose], lw = L[LM.leftWrist], rw = L[LM.rightWrist];
    const k = bodyScale(x.p) * 0.15;
    return vis(n) && vis(lw) && vis(rw) && lw.y < n.y - k && rw.y < n.y - k;
  };
  for (const r of sustainedRuns(s, armsUp, 0.3, () => 0.85)) {
    out.push({ type: 'celebrate', t: r.t, personId: id, confidence: r.strength });
  }

  // Facepalm: one hand resting on the face (and not a two-arms-up celebration).
  const handOnFace = (x: Sample) => {
    const L = x.p.landmarks;
    const n = L[LM.nose];
    if (!vis(n) || armsUp(x)) return false;
    const r = bodyScale(x.p) * 0.35;
    return [L[LM.leftWrist], L[LM.rightWrist]].some((w) => vis(w) && dist(w, n) < r);
  };
  for (const r of sustainedRuns(s, handOnFace, 0.4, () => 0.75)) {
    out.push({ type: 'facepalm', t: r.t, personId: id, confidence: r.strength });
  }

  // Fall: hips drop sharply by most of a torso length, stay down, and the body
  // topples. Sitting down drops the hips too, but keeps the torso upright.
  for (let i = 0; i < s.length; i++) {
    const h0 = stableHip(s[i]);
    // Must start from a whole, upright body: rules out half-visible poses at the frame edge.
    if (!h0 || !feetVisible(s[i].p) || toppled(s[i].p)) continue;
    const scale = bodyScale(s[i].p);
    for (let j = i + 1; j < s.length && s[j].t - s[i].t <= 1.0; j++) {
      const h1 = stableHip(s[j]);
      if (!h1 || h1.y - h0.y < 0.6 * scale) continue;
      const after = s.filter((x) => x.t > s[j].t && x.t <= s[j].t + 0.6);
      const staysDown = after.every((x) => {
        const h = stableHip(x);
        return !h || h.y - h0.y >= 0.4 * scale;
      });
      const fell = [s[j], ...after].some((x) => toppled(x.p));
      if (staysDown && fell) {
        out.push({ type: 'fall', t: s[j].t, personId: id, confidence: clamp01(0.6 + (h1.y - h0.y) / scale / 3), magnitude: clamp01((h1.y - h0.y) / scale) });
        i = j;
        break;
      }
    }
  }

  // Jump: hips rise well above their recent baseline, then come back down.
  for (let i = 0; i < s.length; i++) {
    const h = stableHip(s[i]);
    if (!h || !feetVisible(s[i].p)) continue;
    const base = s.filter((x) => x.t < s[i].t && x.t >= s[i].t - 1).map((x) => stableHip(x)?.y).filter((y): y is number => y !== undefined);
    if (base.length < 2) continue;
    base.sort((a, b) => a - b);
    const baseline = base[Math.floor(base.length / 2)];
    const scale = bodyScale(s[i].p);
    const rise = baseline - h.y;
    if (rise < 0.25 * scale) continue;
    const lands = s.some((x) => x.t > s[i].t && x.t <= s[i].t + 1.2 && (stableHip(x)?.y ?? 0) >= baseline - 0.1 * scale);
    if (lands) {
      out.push({ type: 'jump', t: s[i].t, personId: id, confidence: clamp01(0.55 + rise / scale), magnitude: clamp01(rise / scale) });
      while (i + 1 < s.length && s[i + 1].t - s[i].t < 1.2) i++;
    }
  }

  // Sudden motion: fast head/hand movement sustained over two consecutive
  // intervals (~0.25s). A one-sample spike is almost always pose jitter.
  // Pose estimates also wobble for the first few frames of a track; skip them.
  for (let i = 2; i < s.length; i++) {
    if (s[i - 1].t - s[0].t < 0.4) continue;
    const m1 = motion(s[i - 2], s[i - 1]);
    const m2 = motion(s[i - 1], s[i]);
    const m = Math.min(m1, m2);
    if (m >= 2.5) {
      out.push({ type: 'suddenMotion', t: s[i - 1].t, personId: id, confidence: clamp01(0.4 + m / 10), magnitude: clamp01(m / 6) });
    }
  }
  return out;
}

function detectObjects(id: string, s: Sample[], frames: TrackedFrame[]): SceneEvent[] {
  const out: SceneEvent[] = [];
  const objectsAt = new Map(frames.map((f) => [f.t, f.objects]));

  // Snack: a wrist closes in on food that it was not near a moment ago.
  const nearFood = (x: Sample) => {
    const food = (objectsAt.get(x.t) ?? []).filter((o) => FOOD.has(o.label));
    const wrists = [x.p.landmarks[LM.leftWrist], x.p.landmarks[LM.rightWrist]].filter(vis);
    return food.some((o) => wrists.some((w) => dist(w, center(o.box)) < Math.max(o.box.w, o.box.h) * 0.75 + 0.03));
  };
  for (let i = 1; i < s.length; i++) {
    if (!nearFood(s[i])) continue;
    const before = s.filter((x) => x.t < s[i].t && x.t >= s[i].t - 0.8);
    if (before.length && !before.some(nearFood)) {
      out.push({ type: 'snack', t: s[i].t, personId: id, confidence: 0.8 });
    }
  }

  // Phone: a phone held near the head or in hand for over a second.
  const onPhone = (x: Sample) => {
    const phones = (objectsAt.get(x.t) ?? []).filter((o) => o.label === 'cell phone');
    const n = x.p.landmarks[LM.nose];
    const anchors = [n, x.p.landmarks[LM.leftWrist], x.p.landmarks[LM.rightWrist]].filter(vis);
    const r = bodyScale(x.p) * 0.9;
    return phones.some((o) => anchors.some((a) => dist(a, center(o.box)) < r));
  };
  for (const r of sustainedRuns(s, onPhone, 1.0, () => 0.7)) {
    out.push({ type: 'phone', t: r.t + 1.0, personId: id, confidence: r.strength });
  }
  return out;
}

/** Start/end time of the shot containing each frame time. */
function shotBounds(frames: TrackedFrame[], duration: number): (t: number) => { start: number; end: number } {
  const cuts = [0, ...frames.filter((f) => f.cut).map((f) => f.t), duration];
  return (t) => {
    let i = 0;
    while (i < cuts.length - 2 && cuts[i + 1] <= t) i++;
    return { start: cuts[i], end: cuts[i + 1] };
  };
}

const atEdge = (b: Box) => b.x < 0.04 || b.x + b.w > 0.96;

/**
 * Entrances and exits: someone walks in or out through the side of the frame
 * mid-shot. Appearing at a cut or materializing mid-frame (a detector flicker)
 * doesn't count.
 */
function detectPresence(series: Map<string, Sample[]>, frames: TrackedFrame[], duration: number): SceneEvent[] {
  const out: SceneEvent[] = [];
  const shot = shotBounds(frames, duration);
  for (const [id, s] of series) {
    const first = s[0];
    const last = s[s.length - 1];
    const lifetime = last.t - first.t;
    const big = Math.max(...s.map((x) => x.p.box.h)) >= 0.25;
    if (!big) continue;
    if (lifetime >= 1 && first.t - shot(first.t).start > 0.8 && atEdge(first.p.box)) {
      out.push({ type: 'enter', t: first.t + 0.3, personId: id, confidence: 0.7 });
    }
    if (lifetime >= 1.5 && shot(last.t).end - last.t > 0.8 && atEdge(last.p.box)) {
      out.push({ type: 'leave', t: last.t, personId: id, confidence: 0.65 });
    }
  }
  return out;
}

function detectPets(frames: TrackedFrame[]): SceneEvent[] {
  const out: SceneEvent[] = [];
  const lastFired = new Map<string, number>();
  frames.forEach((f, i) => {
    for (const o of f.objects) {
      if (!PETS.has(o.label) || o.score < 0.5) continue;
      if (f.t - (lastFired.get(o.label) ?? -Infinity) < 10) continue;
      const window = frames.slice(i, i + 5);
      const hits = window.filter((w) => w.objects.some((x) => x.label === o.label && x.score >= 0.4)).length;
      if (hits >= 3) {
        lastFired.set(o.label, f.t);
        out.push({ type: 'pet', t: f.t, confidence: clamp01(o.score + 0.1) });
      }
    }
  });
  return out;
}

function rmsAt(audio: AudioEnvelope, t: number): number {
  const i = Math.round(t * audio.hz);
  return audio.rms[Math.max(0, Math.min(audio.rms.length - 1, i))] ?? 0;
}

function detectLoud(audio: AudioEnvelope): SceneEvent[] {
  const out: SceneEvent[] = [];
  const win = Math.round(2 * audio.hz);
  let lastT = -Infinity;
  for (let i = win; i < audio.rms.length; i++) {
    const v = audio.rms[i];
    if (v < 0.2) continue;
    const prev = Array.from(audio.rms.subarray(i - win, i)).sort((a, b) => a - b);
    const median = prev[Math.floor(prev.length / 2)];
    const t = i / audio.hz;
    if (v >= Math.max(0.2, median * 4) && t - lastT > 3) {
      lastT = t;
      out.push({ type: 'loud', t, confidence: clamp01(0.5 + v), magnitude: clamp01(v * 2) });
    }
  }
  return out;
}

/** A stretch of near-total stillness (and quiet, when audio is available). */
function detectAwkward(frames: TrackedFrame[], series: Map<string, Sample[]>, audio: AudioEnvelope | null): SceneEvent[] {
  const out: SceneEvent[] = [];
  const speedAt = new Map<number, number>();
  for (const s of series.values()) {
    for (let i = 1; i < s.length; i++) {
      speedAt.set(s[i].t, Math.max(speedAt.get(s[i].t) ?? 0, motion(s[i - 1], s[i])));
    }
  }
  let quiet = (_t: number) => true;
  if (audio && audio.rms.length) {
    const sorted = Array.from(audio.rms).sort((a, b) => a - b);
    const median = sorted[Math.floor(sorted.length / 2)];
    quiet = (t) => rmsAt(audio, t) < Math.max(0.02, median * 0.4);
  }
  const minPeople = audio ? 1 : 2;
  const still = (f: TrackedFrame) => f.people.length >= minPeople && (speedAt.get(f.t) ?? 0) < 0.4 && quiet(f.t);

  let start = -1;
  let lastFired = -Infinity;
  frames.forEach((f, i) => {
    if (!still(f) || f.cut) {
      start = still(f) ? i : -1;
      return;
    }
    if (start < 0) start = i;
    const t0 = frames[start].t;
    if (f.t - t0 >= 3 && f.t - lastFired > 8) {
      lastFired = f.t;
      out.push({ type: 'awkward', t: f.t, confidence: 0.6 });
    }
  });
  return out;
}

/** Same-person events of one type closer than this are the same moment. */
const REFRACTORY: Partial<Record<EventType, number>> = { suddenMotion: 2.5, laugh: 4, surprise: 3, snack: 4 };

export function detectEvents(frames: TrackedFrame[], duration: number, audio: AudioEnvelope | null = null): SceneEvent[] {
  const series = seriesByPerson(frames);
  const raw: SceneEvent[] = [];
  for (const [id, s] of series) {
    raw.push(...detectFace(id, s), ...detectPose(id, s), ...detectObjects(id, s, frames));
  }
  raw.push(...detectPresence(series, frames, duration), ...detectPets(frames), ...detectAwkward(frames, series, audio));
  if (audio) raw.push(...detectLoud(audio));

  raw.sort((a, b) => a.t - b.t);
  const kept: SceneEvent[] = [];
  for (const e of raw) {
    const gap = REFRACTORY[e.type] ?? 2;
    const dup = kept.some((k) => k.type === e.type && k.personId === e.personId && e.t - k.t < gap);
    if (!dup) kept.push(e);
  }
  return kept;
}
