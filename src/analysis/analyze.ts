import { analyzeAudio, loadPerception, observe } from './perception';
import { trackPeople } from './tracker';
import { detectEvents } from './detectors';
import { CUT_H, CUT_W, SHIFT_H, SHIFT_W, estimateShift, isCut, makeThumb, type Thumb } from './camera';
import type { AudioEnvelope, FrameObservation, SceneEvent, TrackedFrame } from './types';

export interface Analysis {
  duration: number;
  width: number;
  height: number;
  frames: TrackedFrame[];
  events: SceneEvent[];
  audio: AudioEnvelope | null;
  delegate: 'GPU' | 'CPU';
}

export type Progress = { stage: 'models' | 'audio' | 'frames' | 'events'; fraction: number };

/** Analysis sample rate. Enough to catch a facepalm or a fall without taking ages. */
const SAMPLE_FPS = 8;

/**
 * MediaPipe VIDEO mode needs strictly increasing timestamps for the lifetime of
 * a model instance, which outlives a single video. Keep one monotonic clock.
 */
let clockMs = 0;

function grab(ctx: CanvasRenderingContext2D, video: HTMLVideoElement): Uint8ClampedArray {
  const { width: w, height: h } = ctx.canvas;
  ctx.drawImage(video, 0, 0, w, h);
  return ctx.getImageData(0, 0, w, h).data;
}

function seek(video: HTMLVideoElement, t: number): Promise<void> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      cleanup();
      reject(new Error(`Seeking to ${t.toFixed(2)}s timed out`));
    }, 8000);
    const done = () => {
      cleanup();
      resolve();
    };
    const cleanup = () => {
      clearTimeout(timer);
      video.removeEventListener('seeked', done);
    };
    video.addEventListener('seeked', done);
    video.currentTime = t;
  });
}

export function loadVideo(url: string): Promise<HTMLVideoElement> {
  return new Promise((resolve, reject) => {
    const video = document.createElement('video');
    video.muted = true;
    video.playsInline = true;
    video.preload = 'auto';
    video.crossOrigin = 'anonymous';
    video.onloadeddata = () => resolve(video);
    video.onerror = () => reject(new Error("This browser can't decode that video. Try an MP4 (H.264) or WebM file."));
    video.src = url;
  });
}

export async function analyzeVideo(file: File, url: string, onProgress: (p: Progress) => void, signal?: AbortSignal): Promise<Analysis> {
  onProgress({ stage: 'models', fraction: 0 });
  const [perception, video] = await Promise.all([loadPerception(), loadVideo(url)]);

  onProgress({ stage: 'audio', fraction: 0 });
  const audio = await analyzeAudio(file);

  const duration = video.duration;
  const observations: FrameObservation[] = [];
  const steps = Math.max(1, Math.floor(duration * SAMPLE_FPS));
  clockMs += 10_000; // a gap between videos so trackers don't link them
  const ctxOf = (w: number, h: number) =>
    Object.assign(document.createElement('canvas'), { width: w, height: h }).getContext('2d', { willReadFrequently: true })!;
  const shiftCtx = ctxOf(SHIFT_W, SHIFT_H);
  const cutCtx = ctxOf(CUT_W, CUT_H);
  let prevThumb: Thumb | null = null;
  let camera = { x: 0, y: 0 };
  for (let i = 0; i <= steps; i++) {
    if (signal?.aborted) throw new DOMException('Analysis cancelled', 'AbortError');
    const t = Math.min(duration - 0.01, i / SAMPLE_FPS);
    await seek(video, t);
    const obs = observe(perception, video, t, (clockMs += 1000 / SAMPLE_FPS));
    const thumb = makeThumb(grab(shiftCtx, video), grab(cutCtx, video));
    obs.cut = prevThumb !== null && isCut(prevThumb, thumb);
    if (obs.cut || !prevThumb) {
      camera = { x: 0, y: 0 };
    } else {
      const d = estimateShift(prevThumb.luma, thumb.luma);
      camera = { x: camera.x + d.x, y: camera.y + d.y };
    }
    obs.camera = camera;
    prevThumb = thumb;
    observations.push(obs);
    onProgress({ stage: 'frames', fraction: i / steps });
  }
  const width = video.videoWidth;
  const height = video.videoHeight;
  video.removeAttribute('src');
  video.load();

  onProgress({ stage: 'events', fraction: 0 });
  const frames = trackPeople(observations);
  const events = detectEvents(frames, duration, audio);
  return { duration, width, height, frames, events, audio, delegate: perception.delegate };
}
