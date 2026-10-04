import type { TrackedFrame } from '../analysis/types';
import { drawStickers, type ScheduledSticker } from './compositor';

export interface ExportResult {
  blob: Blob;
  extension: 'mp4' | 'webm';
}

const MIME_CANDIDATES = [
  'video/mp4;codecs=avc1.42E01E,mp4a.40.2',
  'video/mp4',
  'video/webm;codecs=vp9,opus',
  'video/webm;codecs=vp8,opus',
  'video/webm',
];

export function pickMimeType(isSupported: (m: string) => boolean = (m) => MediaRecorder.isTypeSupported(m)): string | null {
  return MIME_CANDIDATES.find(isSupported) ?? null;
}

export function canExport(): boolean {
  return typeof MediaRecorder !== 'undefined' && typeof HTMLCanvasElement.prototype.captureStream === 'function' && pickMimeType() !== null;
}

/**
 * Re-plays the video once in real time, compositing stickers onto a canvas and
 * recording canvas + original audio with MediaRecorder. Real-time is the price
 * of zero dependencies and zero uploads; a 30s clip takes ~30s.
 */
export async function exportVideo(
  url: string,
  items: ScheduledSticker[],
  frames: TrackedFrame[],
  onProgress: (fraction: number) => void,
  signal?: AbortSignal,
): Promise<ExportResult> {
  const mimeType = pickMimeType();
  if (!mimeType) throw new Error("This browser can't record video. Try a recent Chrome, Edge, Firefox or Safari.");

  const video = document.createElement('video');
  video.src = url;
  video.playsInline = true;
  video.crossOrigin = 'anonymous';
  await new Promise<void>((resolve, reject) => {
    video.onloadedmetadata = () => resolve();
    video.onerror = () => reject(new Error('Could not reload the video for export.'));
  });

  const W = video.videoWidth;
  const H = video.videoHeight;
  const downscale = Math.min(1, 1920 / Math.max(W, H));
  const canvas = document.createElement('canvas');
  canvas.width = Math.round(W * downscale) & ~1;
  canvas.height = Math.round(H * downscale) & ~1;
  const ctx = canvas.getContext('2d')!;

  const stream = canvas.captureStream(30);
  const audioCtx = new AudioContext();
  try {
    const src = audioCtx.createMediaElementSource(video);
    const dest = audioCtx.createMediaStreamDestination();
    src.connect(dest); // intentionally not connected to speakers
    dest.stream.getAudioTracks().forEach((track) => stream.addTrack(track));
  } catch {
    // No audio track or capture unsupported: export silently rather than fail.
  }

  const recorder = new MediaRecorder(stream, { mimeType, videoBitsPerSecond: 8_000_000 });
  const chunks: Blob[] = [];
  recorder.ondataavailable = (e) => e.data.size && chunks.push(e.data);
  const stopped = new Promise<void>((resolve) => (recorder.onstop = () => resolve()));

  const draw = (t: number) => {
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
    ctx.setTransform(downscale, 0, 0, downscale, 0, 0);
    drawStickers(ctx, items, frames, t, W, H);
    onProgress(Math.min(1, t / video.duration));
  };

  let running = true;
  const hasRVFC = 'requestVideoFrameCallback' in video;
  const loop = () => {
    if (!running) return;
    draw(video.currentTime);
    if (hasRVFC) video.requestVideoFrameCallback(loop);
    else requestAnimationFrame(loop);
  };

  const finished = new Promise<void>((resolve, reject) => {
    video.onended = () => resolve();
    video.onerror = () => reject(new Error('Playback failed during export.'));
    signal?.addEventListener('abort', () => reject(new DOMException('Export cancelled', 'AbortError')));
    // If the browser refuses or stalls playback (autoplay policy, background
    // tab), fail with a clear message instead of sitting at 0% forever.
    let last = -1;
    const watchdog = setInterval(() => {
      if (!running) return clearInterval(watchdog);
      if (video.currentTime === last && !video.ended) {
        clearInterval(watchdog);
        reject(new Error('Playback stalled. Keep this tab in front and click Download again.'));
      }
      last = video.currentTime;
    }, 6000);
  });
  finished.catch(() => {}); // observed below; avoid unhandled-rejection noise if play() fails first

  try {
    video.currentTime = 0;
    draw(0);
    recorder.start(1000);
    await Promise.race([video.play(), finished]);
    loop();
    await finished;
    draw(video.duration);
  } finally {
    running = false;
    video.pause();
    if (recorder.state !== 'inactive') recorder.stop();
    await stopped;
    stream.getTracks().forEach((track) => track.stop());
    void audioCtx.close();
  }

  return { blob: new Blob(chunks, { type: mimeType.split(';')[0] }), extension: mimeType.startsWith('video/mp4') ? 'mp4' : 'webm' };
}
