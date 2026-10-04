import { LM, type FaceCues, type FrameObservation, type Landmark, type PersonObservation } from './types';

/**
 * Synthetic people for tests. A standing person centered at (cx, hipY) with a
 * torso of length `torso`; overrides move individual landmarks.
 */
export interface PoseSpec {
  cx?: number;
  hipY?: number;
  torso?: number;
  armsUp?: boolean;
  handOnFace?: boolean;
  face?: Partial<FaceCues>;
  /** Rotate the whole body about the hips (radians), e.g. PI/2 = lying down. */
  tilt?: number;
}

export function person(spec: PoseSpec = {}): PersonObservation {
  const cx = spec.cx ?? 0.5;
  const hipY = spec.hipY ?? 0.6;
  const k = spec.torso ?? 0.2;
  const lms: Landmark[] = Array.from({ length: 33 }, () => ({ x: cx, y: hipY, v: 0.1 }));
  const set = (i: number, x: number, y: number) => (lms[i] = { x, y, v: 0.95 });

  const shoulderY = hipY - k;
  const noseY = shoulderY - k * 0.45;
  set(LM.nose, cx, noseY);
  set(LM.leftEye, cx - k * 0.08, noseY - k * 0.06);
  set(LM.rightEye, cx + k * 0.08, noseY - k * 0.06);
  set(LM.leftShoulder, cx - k * 0.4, shoulderY);
  set(LM.rightShoulder, cx + k * 0.4, shoulderY);
  set(LM.leftHip, cx - k * 0.25, hipY);
  set(LM.rightHip, cx + k * 0.25, hipY);
  set(LM.leftAnkle, cx - k * 0.25, hipY + k * 1.6);
  set(LM.rightAnkle, cx + k * 0.25, hipY + k * 1.6);

  if (spec.armsUp) {
    set(LM.leftWrist, cx - k * 0.5, noseY - k * 0.6);
    set(LM.rightWrist, cx + k * 0.5, noseY - k * 0.6);
  } else if (spec.handOnFace) {
    set(LM.leftWrist, cx - k * 0.05, noseY + k * 0.02);
    set(LM.rightWrist, cx + k * 0.45, hipY);
  } else {
    set(LM.leftWrist, cx - k * 0.45, hipY);
    set(LM.rightWrist, cx + k * 0.45, hipY);
  }

  if (spec.tilt) {
    const c = Math.cos(spec.tilt), sn = Math.sin(spec.tilt);
    for (const l of lms) {
      const dx = l.x - cx, dy = l.y - hipY;
      l.x = cx + dx * c - dy * sn;
      l.y = hipY + dx * sn + dy * c;
    }
  }

  const seen = lms.filter((l) => l.v >= 0.5);
  const xs = seen.map((l) => l.x);
  const ys = seen.map((l) => l.y);
  const box = { x: Math.min(...xs), y: Math.min(...ys), w: Math.max(...xs) - Math.min(...xs), h: Math.max(...ys) - Math.min(...ys) };

  const face: FaceCues | undefined = spec.face
    ? { smile: 0, jawOpen: 0, eyeWide: 0, browUp: 0, center: { x: cx, y: noseY }, ...spec.face }
    : undefined;
  return { landmarks: lms, box, face };
}

/** Builds frames at `fps` for `seconds`, calling `at(t)` for each frame's people. */
export function timeline(seconds: number, fps: number, at: (t: number) => PersonObservation[], objects: (t: number) => FrameObservation['objects'] = () => []): FrameObservation[] {
  const out: FrameObservation[] = [];
  for (let i = 0; i <= Math.round(seconds * fps); i++) {
    const t = i / fps;
    out.push({ t, people: at(t), objects: objects(t) });
  }
  return out;
}
