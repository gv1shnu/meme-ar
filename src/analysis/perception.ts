import {
  FaceLandmarker,
  FilesetResolver,
  ObjectDetector,
  PoseLandmarker,
  type Classifications,
  type NormalizedLandmark,
} from '@mediapipe/tasks-vision';
import type { AudioEnvelope, FaceCues, FrameObservation, Landmark, ObjectObservation, PersonObservation } from './types';

/**
 * On-device perception with MediaPipe: pose (bodies), face landmarks with
 * blendshapes (expressions), and COCO object detection. Runs entirely in the
 * browser — the video is never uploaded anywhere.
 */

const MODELS = 'https://storage.googleapis.com/mediapipe-models';
const POSE_MODEL = `${MODELS}/pose_landmarker/pose_landmarker_lite/float16/1/pose_landmarker_lite.task`;
const FACE_MODEL = `${MODELS}/face_landmarker/face_landmarker/float16/1/face_landmarker.task`;
const OBJECT_MODEL = `${MODELS}/object_detector/efficientdet_lite0/float16/1/efficientdet_lite0.tflite`;

export interface Perception {
  pose: PoseLandmarker;
  face: FaceLandmarker;
  /** Single-face, IMAGE-mode landmarker for upscaled head crops of distant people. */
  faceCrop: FaceLandmarker;
  objects: ObjectDetector;
  delegate: 'GPU' | 'CPU';
  close(): void;
}

async function createAll(delegate: 'GPU' | 'CPU'): Promise<Perception> {
  const fileset = await FilesetResolver.forVisionTasks(`${import.meta.env.BASE_URL}mediapipe/wasm`);
  const [pose, face, faceCrop, objects] = await Promise.all([
    PoseLandmarker.createFromOptions(fileset, {
      baseOptions: { modelAssetPath: POSE_MODEL, delegate },
      runningMode: 'VIDEO',
      numPoses: 4,
      minPoseDetectionConfidence: 0.5,
      minTrackingConfidence: 0.5,
    }),
    FaceLandmarker.createFromOptions(fileset, {
      baseOptions: { modelAssetPath: FACE_MODEL, delegate },
      runningMode: 'VIDEO',
      numFaces: 4,
      outputFaceBlendshapes: true,
    }),
    FaceLandmarker.createFromOptions(fileset, {
      baseOptions: { modelAssetPath: FACE_MODEL, delegate },
      runningMode: 'IMAGE',
      numFaces: 1,
      outputFaceBlendshapes: true,
    }),
    ObjectDetector.createFromOptions(fileset, {
      baseOptions: { modelAssetPath: OBJECT_MODEL, delegate },
      runningMode: 'VIDEO',
      scoreThreshold: 0.4,
      maxResults: 10,
    }),
  ]);
  return {
    pose,
    face,
    faceCrop,
    objects,
    delegate,
    close() {
      pose.close();
      face.close();
      faceCrop.close();
      objects.close();
    },
  };
}

let shared: Promise<Perception> | null = null;

export function loadPerception(): Promise<Perception> {
  shared ??= createAll('GPU').catch(() => createAll('CPU'));
  shared.catch(() => (shared = null));
  return shared;
}

function blend(c: Classifications | undefined, name: string): number {
  return c?.categories.find((x) => x.categoryName === name)?.score ?? 0;
}

function toCues(landmarks: NormalizedLandmark[], c: Classifications | undefined): FaceCues {
  const center = landmarks.reduce((a, l) => ({ x: a.x + l.x / landmarks.length, y: a.y + l.y / landmarks.length }), { x: 0, y: 0 });
  return {
    smile: (blend(c, 'mouthSmileLeft') + blend(c, 'mouthSmileRight')) / 2,
    jawOpen: blend(c, 'jawOpen'),
    eyeWide: (blend(c, 'eyeWideLeft') + blend(c, 'eyeWideRight')) / 2,
    browUp: blend(c, 'browInnerUp'),
    center,
  };
}

function toPerson(landmarks: NormalizedLandmark[]): PersonObservation | null {
  const lms: Landmark[] = landmarks.map((l) => ({ x: l.x, y: l.y, v: l.visibility ?? 1 }));
  const seen = lms.filter((l) => l.v >= 0.5);
  if (seen.length < 5) return null;
  const xs = seen.map((l) => l.x);
  const ys = seen.map((l) => l.y);
  const minX = Math.max(0, Math.min(...xs));
  const maxX = Math.min(1, Math.max(...xs));
  const maxY = Math.min(1, Math.max(...ys));
  // Pose landmarks stop at the eyes/ears; extend the box up to cover the top of the head.
  const rawMinY = Math.min(...ys);
  const minY = Math.max(0, rawMinY - (maxY - rawMinY) * 0.08);
  return { landmarks: lms, box: { x: minX, y: minY, w: maxX - minX, h: maxY - minY } };
}

/** Attach each detected face to the body whose nose is closest to it. */
function attachFaces(people: PersonObservation[], faces: FaceCues[]) {
  const free = new Set(people.keys());
  for (const f of faces) {
    let best = -1;
    let bestD = Infinity;
    for (const i of free) {
      const n = people[i].landmarks[0];
      const d = Math.hypot(n.x - f.center.x, n.y - f.center.y);
      if (d < bestD && d < Math.max(0.08, people[i].box.h * 0.2)) {
        bestD = d;
        best = i;
      }
    }
    if (best >= 0) {
      people[best].face = f;
      free.delete(best);
    }
  }
}

const CROP = 256;
let cropCanvas: HTMLCanvasElement | null = null;

/**
 * The face model is a short-range (selfie) detector and misses faces more than
 * a couple of meters away. For bodies that got no face, crop around the head
 * (located by the pose model), upscale, and look again.
 */
function faceFromHeadCrop(p: Perception, video: HTMLVideoElement, person: PersonObservation): FaceCues | undefined {
  const L = person.landmarks;
  const nose = L[0], ls = L[11], rs = L[12], lh = L[23], rh = L[24];
  if (nose.v < 0.5) return undefined;
  const W = video.videoWidth;
  const H = video.videoHeight;
  // Head size from torso length (stable when the person turns side-on) or
  // shoulder width (when hips are out of frame), whichever is larger.
  const px = (a: Landmark, b: Landmark) => Math.hypot((a.x - b.x) * W, (a.y - b.y) * H);
  const shoulders = ls.v >= 0.5 && rs.v >= 0.5 ? px(ls, rs) * 1.2 : 0;
  const torso =
    ls.v >= 0.5 && rs.v >= 0.5 && lh.v >= 0.5 && rh.v >= 0.5
      ? px({ x: (ls.x + rs.x) / 2, y: (ls.y + rs.y) / 2, v: 1 }, { x: (lh.x + rh.x) / 2, y: (lh.y + rh.y) / 2, v: 1 }) * 0.8
      : 0;
  const side = Math.max(shoulders, torso, person.box.h * H * 0.22);
  // Too small to read an expression even when upscaled.
  if (side < 40) return undefined;

  cropCanvas ??= Object.assign(document.createElement('canvas'), { width: CROP, height: CROP });
  const ctx = cropCanvas.getContext('2d')!;
  ctx.clearRect(0, 0, CROP, CROP);
  ctx.drawImage(video, nose.x * W - side / 2, nose.y * H - side / 2, side, side, 0, 0, CROP, CROP);
  const res = p.faceCrop.detect(cropCanvas);
  if (!res.faceLandmarks.length) return undefined;
  return { ...toCues(res.faceLandmarks[0], res.faceBlendshapes?.[0]), center: { x: nose.x, y: nose.y } };
}

export function observe(p: Perception, video: HTMLVideoElement, t: number, timestampMs: number): FrameObservation {
  const W = video.videoWidth;
  const H = video.videoHeight;

  const pose = p.pose.detectForVideo(video, timestampMs);
  const people = pose.landmarks.map(toPerson).filter((x): x is PersonObservation => x !== null);

  const face = p.face.detectForVideo(video, timestampMs);
  attachFaces(people, face.faceLandmarks.map((lm, i) => toCues(lm, face.faceBlendshapes?.[i])));
  for (const person of people) {
    person.face ??= faceFromHeadCrop(p, video, person);
  }

  const det = p.objects.detectForVideo(video, timestampMs);
  const objects: ObjectObservation[] = det.detections
    .filter((d) => d.boundingBox && d.categories[0] && d.categories[0].categoryName !== 'person')
    .map((d) => {
      const b = d.boundingBox!;
      return {
        label: d.categories[0].categoryName,
        score: d.categories[0].score,
        box: { x: b.originX / W, y: b.originY / H, w: b.width / W, h: b.height / H },
      };
    });

  return { t, people, objects };
}

/** Decodes the soundtrack and returns its loudness envelope, or null if there is none. */
export async function analyzeAudio(file: Blob, hz = 20): Promise<AudioEnvelope | null> {
  const Ctx = window.OfflineAudioContext;
  try {
    const ctx = new Ctx(1, 1, 44100);
    const buffer = await ctx.decodeAudioData(await file.arrayBuffer());
    const n = buffer.numberOfChannels;
    const win = Math.max(1, Math.floor(buffer.sampleRate / hz));
    const len = Math.floor(buffer.length / win);
    const rms = new Float32Array(len);
    const channels = Array.from({ length: n }, (_, c) => buffer.getChannelData(c));
    for (let i = 0; i < len; i++) {
      let sum = 0;
      for (let j = i * win; j < (i + 1) * win; j++) {
        let s = 0;
        for (const ch of channels) s += ch[j];
        s /= n;
        sum += s * s;
      }
      rms[i] = Math.sqrt(sum / win);
    }
    return { hz, rms };
  } catch {
    return null;
  }
}
