import { useEffect, useRef, type RefObject } from 'react';
import type { TrackedFrame } from '../analysis/types';
import { drawStickers, type ScheduledSticker } from '../render/compositor';

interface Props {
  url: string;
  videoRef: RefObject<HTMLVideoElement | null>;
  items: ScheduledSticker[];
  frames: TrackedFrame[];
  videoW: number;
  videoH: number;
}

/**
 * The original <video> with a transparent canvas laid exactly over its picture.
 * Stickers are redrawn every animation frame from the video's current time, so
 * scrubbing, pausing and seeking all just work.
 */
export function Player({ url, videoRef, items, frames, videoW, videoH }: Props) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const itemsRef = useRef(items);
  itemsRef.current = items;
  const drawRef = useRef<() => void>(() => {});

  useEffect(() => {
    const video = videoRef.current;
    const canvas = canvasRef.current;
    if (!video || !canvas || !videoW || !videoH) return;
    const ctx = canvas.getContext('2d')!;
    let raf = 0;

    const draw = () => {
      // Match the canvas to the letterboxed picture area inside the <video> box.
      const boxW = video.clientWidth;
      const boxH = video.clientHeight;
      const fit = Math.min(boxW / videoW, boxH / videoH);
      const w = videoW * fit;
      const h = videoH * fit;
      const dpr = window.devicePixelRatio || 1;
      canvas.style.width = `${w}px`;
      canvas.style.height = `${h}px`;
      canvas.style.left = `${(boxW - w) / 2}px`;
      canvas.style.top = `${(boxH - h) / 2}px`;
      if (canvas.width !== Math.round(w * dpr) || canvas.height !== Math.round(h * dpr)) {
        canvas.width = Math.round(w * dpr);
        canvas.height = Math.round(h * dpr);
      }
      ctx.setTransform(1, 0, 0, 1, 0, 0);
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      ctx.setTransform(fit * dpr, 0, 0, fit * dpr, 0, 0);
      drawStickers(ctx, itemsRef.current, frames, video.currentTime, videoW, videoH);
    };
    drawRef.current = draw;
    const loop = () => {
      draw();
      raf = requestAnimationFrame(loop);
    };
    raf = requestAnimationFrame(loop);

    // Media events and resizes also redraw, so a paused or just-seeked frame is
    // correct even when animation frames are throttled (background tabs).
    const events = ['seeked', 'timeupdate', 'loadeddata', 'pause'] as const;
    events.forEach((e) => video.addEventListener(e, draw));
    const resize = new ResizeObserver(draw);
    resize.observe(video);
    return () => {
      cancelAnimationFrame(raf);
      events.forEach((e) => video.removeEventListener(e, draw));
      resize.disconnect();
    };
  }, [videoRef, frames, videoW, videoH]);

  // Swapping or removing a sticker while paused should show up immediately.
  useEffect(() => drawRef.current(), [items]);

  return (
    <div className="player">
      <video ref={videoRef} src={url} controls playsInline />
      <canvas ref={canvasRef} className="overlay" aria-hidden="true" />
    </div>
  );
}
