import type { ScheduledSticker } from '../render/compositor';
import { EVENT_LABEL, REACTIONS } from '../stickers/catalog';

interface Props {
  items: ScheduledSticker[];
  busyId: string | null;
  onSeek(t: number): void;
  onSwap(id: string): void;
  onRemove(id: string): void;
}

export const formatTime = (t: number) => {
  const m = Math.floor(t / 60);
  const s = (t % 60).toFixed(1).padStart(4, '0');
  return `${m}:${s}`;
};

export function ReactionList({ items, busyId, onSeek, onSwap, onRemove }: Props) {
  if (!items.length) {
    return (
      <p className="empty">
        No reactions in this one. Punchline reacts to faces, bodies and moments: surprises, laughs, facepalms, falls, snacks and pets.
        Try a clip with people in frame, or turn up the intensity.
      </p>
    );
  }

  return (
    <ol className="reactions">
      {items.map(({ reaction: r, sticker }) => (
        <li key={r.id} className={busyId === r.id ? 'busy' : undefined}>
          <button className="thumb" onClick={() => onSeek(Math.max(0, r.event.t - 1))} title="Jump to this moment">
            {sticker.thumb ? <img src={sticker.thumb} alt="" loading="lazy" /> : <span>{REACTIONS[r.event.type].emoji}</span>}
          </button>
          <div className="meta">
            <strong>{EVENT_LABEL[r.event.type]}</strong>
            <span>
              {formatTime(r.start)}
              {r.event.personId ? ` · ${r.event.personId}` : ''} · {r.timing === 'instant' ? 'instant' : `+${r.delayMs}ms beat`}
            </span>
          </div>
          <div className="actions">
            <button onClick={() => onSwap(r.id)} disabled={busyId === r.id} title="Try a different sticker">
              Swap
            </button>
            <button onClick={() => onRemove(r.id)} title="Remove this reaction" aria-label="Remove">
              ✕
            </button>
          </div>
        </li>
      ))}
    </ol>
  );
}
