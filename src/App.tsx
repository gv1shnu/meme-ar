import { useCallback, useEffect, useRef, useState } from 'react';
import { analyzeVideo, loadVideo, type Analysis, type Progress } from './analysis/analyze';
import { chooseLayout } from './analysis/placement';
import { planReactions } from './analysis/planner';
import type { ScheduledSticker } from './render/compositor';
import { canExport, exportVideo } from './render/exporter';
import { StickerLoader, type GiphyStatus } from './stickers/loader';
import { Player } from './components/Player';
import { ReactionList } from './components/ReactionList';

const MAX_SECONDS = 180;
const MAX_BYTES = 500 * 1024 * 1024;

const INTENSITY = {
  chill: { label: 'Chill', secondsPerReaction: 7 },
  normal: { label: 'Normal', secondsPerReaction: 4 },
  chaos: { label: 'Chaos', secondsPerReaction: 2.2 },
} as const;
type Intensity = keyof typeof INTENSITY;

const RATINGS = [
  { value: 'g', label: 'G' },
  { value: 'pg', label: 'PG' },
  { value: 'pg-13', label: 'PG-13' },
];

type Phase = 'idle' | 'analyzing' | 'stickers' | 'ready' | 'exporting';

const STAGE_TEXT: Record<Progress['stage'], string> = {
  models: 'Loading on-device models…',
  audio: 'Listening to the soundtrack…',
  frames: 'Watching every moment…',
  events: 'Finding the funny…',
};

async function mapLimit<T, R>(items: T[], limit: number, fn: (item: T) => Promise<R>): Promise<R[]> {
  const out: R[] = new Array(items.length);
  let next = 0;
  await Promise.all(
    Array.from({ length: Math.min(limit, items.length) }, async () => {
      while (next < items.length) {
        const i = next++;
        out[i] = await fn(items[i]);
      }
    }),
  );
  return out;
}

export default function App() {
  const [phase, setPhase] = useState<Phase>('idle');
  const [file, setFile] = useState<File | null>(null);
  const [url, setUrl] = useState<string | null>(null);
  const [analysis, setAnalysis] = useState<Analysis | null>(null);
  const [progress, setProgress] = useState<Progress | null>(null);
  const [items, setItems] = useState<ScheduledSticker[]>([]);
  const [intensity, setIntensity] = useState<Intensity>('normal');
  const [rating, setRating] = useState('pg-13');
  const [seed, setSeed] = useState(() => Math.floor(Math.random() * 1e9));
  const [giphy, setGiphy] = useState<GiphyStatus>('unknown');
  const [error, setError] = useState<string | null>(null);
  const [exportProgress, setExportProgress] = useState(0);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [dragging, setDragging] = useState(false);

  const videoRef = useRef<HTMLVideoElement>(null);
  const abortRef = useRef<AbortController | null>(null);
  const loaderRef = useRef<StickerLoader | null>(null);

  useEffect(() => () => void (url && URL.revokeObjectURL(url)), [url]);

  const reset = () => {
    abortRef.current?.abort();
    setPhase('idle');
    setFile(null);
    setUrl(null);
    setAnalysis(null);
    setItems([]);
    setError(null);
  };

  const onFile = async (f: File) => {
    setError(null);
    if (!f.type.startsWith('video/')) return setError("That doesn't look like a video file.");
    if (f.size > MAX_BYTES) return setError('That video is over 500 MB. Trim it down and try again.');

    const objectUrl = URL.createObjectURL(f);
    try {
      const probe = await loadVideo(objectUrl);
      const tooLong = probe.duration > MAX_SECONDS;
      probe.removeAttribute('src');
      if (tooLong) {
        URL.revokeObjectURL(objectUrl);
        return setError(`Videos up to ${MAX_SECONDS / 60} minutes for now. Trim it and try again.`);
      }
    } catch (e) {
      URL.revokeObjectURL(objectUrl);
      return setError((e as Error).message);
    }

    setFile(f);
    setUrl(objectUrl);
    setPhase('analyzing');
    const abort = new AbortController();
    abortRef.current = abort;
    try {
      const result = await analyzeVideo(f, objectUrl, setProgress, abort.signal);
      if (import.meta.env.DEV) Object.assign(window, { __analysis: result });
      setAnalysis(result);
    } catch (e) {
      if ((e as Error).name === 'AbortError') return;
      setError(`Analysis failed: ${(e as Error).message}`);
      setPhase('idle');
      setUrl(null);
      setFile(null);
    }
  };

  // (Re)build the reaction schedule whenever the analysis or the knobs change.
  useEffect(() => {
    if (!analysis) return;
    let cancelled = false;
    (async () => {
      setPhase('stickers');
      const plan = planReactions(analysis.events, analysis.duration, {
        seed,
        secondsPerReaction: INTENSITY[intensity].secondsPerReaction,
      });
      const loader = new StickerLoader({ rating, seed });
      loaderRef.current = loader;
      const built = await mapLimit(plan, 4, async (reaction) => ({
        reaction,
        layout: chooseLayout(analysis.frames, reaction),
        sticker: await loader.load(reaction),
      }));
      if (cancelled) return;
      setGiphy(loader.status);
      setItems(built);
      setPhase('ready');
    })();
    return () => {
      cancelled = true;
    };
  }, [analysis, intensity, rating, seed]);

  const swap = useCallback(async (id: string) => {
    const loader = loaderRef.current;
    const item = items.find((x) => x.reaction.id === id);
    if (!loader || !item) return;
    setBusyId(id);
    const sticker = await loader.load(item.reaction);
    setItems((prev) => prev.map((x) => (x.reaction.id === id ? { ...x, sticker } : x)));
    setBusyId(null);
  }, [items]);

  const remove = (id: string) => setItems((prev) => prev.filter((x) => x.reaction.id !== id));

  const seekTo = (t: number) => {
    const v = videoRef.current;
    if (!v) return;
    v.currentTime = t;
    void v.play();
  };

  const runExport = async () => {
    if (!url || !analysis || !file) return;
    videoRef.current?.pause();
    setPhase('exporting');
    setExportProgress(0);
    const abort = new AbortController();
    abortRef.current = abort;
    try {
      const { blob, extension } = await exportVideo(url, items, analysis.frames, setExportProgress, abort.signal);
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = `${file.name.replace(/\.[^.]+$/, '')}-punchline.${extension}`;
      a.click();
      setTimeout(() => URL.revokeObjectURL(a.href), 60_000);
    } catch (e) {
      if ((e as Error).name !== 'AbortError') setError(`Export failed: ${(e as Error).message}`);
    } finally {
      setPhase('ready');
    }
  };

  const onDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setDragging(false);
    const f = e.dataTransfer.files[0];
    if (f) void onFile(f);
  };

  const pct = (x: number) => `${Math.round(x * 100)}%`;
  const hasGiphy = items.some((x) => x.sticker.source === 'giphy');

  return (
    <div className="app">
      <header>
        <button className="brand" onClick={reset} aria-label="Start over">
          <span className="logo">🥁</span> Punchline
        </button>
        <span className="tag">Drop a video. Get the memes. Perfect timing.</span>
      </header>

      <main>
        {error && (
          <div className="error" role="alert">
            {error}
            <button onClick={() => setError(null)} aria-label="Dismiss">✕</button>
          </div>
        )}

        {phase === 'idle' && (
          <label
            className={`drop ${dragging ? 'over' : ''}`}
            onDragOver={(e) => {
              e.preventDefault();
              setDragging(true);
            }}
            onDragLeave={() => setDragging(false)}
            onDrop={onDrop}
          >
            <input type="file" accept="video/*" onChange={(e) => e.target.files?.[0] && onFile(e.target.files[0])} hidden />
            <div className="drop-emoji">🎬</div>
            <h1>Drop a video here</h1>
            <p>or click to choose one · MP4, MOV or WebM · up to 3 minutes</p>
            <p className="privacy">🔒 Your video never leaves this device. All analysis runs in your browser.</p>
          </label>
        )}

        {phase === 'analyzing' && (
          <section className="progress-card">
            <div className="spinner" />
            <h2>{progress ? STAGE_TEXT[progress.stage] : 'Starting…'}</h2>
            {progress?.stage === 'frames' && (
              <div className="bar">
                <div style={{ width: pct(progress.fraction) }} />
              </div>
            )}
            <p className="hint">First run downloads ~17 MB of models; after that it's cached.</p>
            <button className="ghost" onClick={reset}>Cancel</button>
          </section>
        )}

        {analysis && url && (phase === 'stickers' || phase === 'ready' || phase === 'exporting') && (
          <section className="studio">
            <div className="stage">
              <Player url={url} videoRef={videoRef} items={items} frames={analysis.frames} videoW={analysis.width} videoH={analysis.height} />
              {phase === 'exporting' && (
                <div className="exporting">
                  <h2>Rendering your video… {pct(exportProgress)}</h2>
                  <div className="bar">
                    <div style={{ width: pct(exportProgress) }} />
                  </div>
                  <p className="hint">Plays through once in real time. Keep this tab open.</p>
                  <button className="ghost" onClick={() => abortRef.current?.abort()}>Cancel</button>
                </div>
              )}
            </div>

            <aside className="panel">
              <div className="controls">
                <div className="segmented" role="radiogroup" aria-label="Intensity">
                  {(Object.keys(INTENSITY) as Intensity[]).map((k) => (
                    <button key={k} role="radio" aria-checked={intensity === k} className={intensity === k ? 'on' : ''} onClick={() => setIntensity(k)} disabled={phase !== 'ready'}>
                      {INTENSITY[k].label}
                    </button>
                  ))}
                </div>
                <label className="rating">
                  Rating
                  <select value={rating} onChange={(e) => setRating(e.target.value)} disabled={phase !== 'ready'}>
                    {RATINGS.map((r) => (
                      <option key={r.value} value={r.value}>{r.label}</option>
                    ))}
                  </select>
                </label>
                <button onClick={() => setSeed((s) => s + 1)} disabled={phase !== 'ready'} title="Re-pick every sticker and beat">
                  🎲 Remix
                </button>
              </div>

              <h3>
                {phase === 'stickers' ? 'Fetching stickers…' : `${items.length} reaction${items.length === 1 ? '' : 's'}`}
                <small>{analysis.events.length} moments found</small>
              </h3>
              {phase !== 'stickers' && <ReactionList items={items} busyId={busyId} onSeek={seekTo} onSwap={swap} onRemove={remove} />}

              {giphy === 'unavailable' && <p className="note">GIPHY isn't configured on this server, so built-in emoji stickers are used.</p>}

              <button className="primary" onClick={runExport} disabled={phase !== 'ready' || !items.length || !canExport()}>
                ⬇ Download video
              </button>
              {!canExport() && <p className="note">This browser can't record video. Try a recent Chrome, Edge, Firefox or Safari.</p>}
            </aside>
          </section>
        )}
      </main>

      <footer>
        {hasGiphy && (
          <a href="https://giphy.com" target="_blank" rel="noreferrer" className="giphy">
            Powered by GIPHY
          </a>
        )}
        <span>On-device analysis with MediaPipe · nothing is uploaded</span>
      </footer>
    </div>
  );
}
