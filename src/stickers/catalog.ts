import type { EventType } from '../analysis/types';

/**
 * What each event type looks like as a reaction. `queries` are GIPHY sticker
 * searches; the server only proxies queries listed here, so the API key cannot
 * be used as an open search proxy. `emoji` + `caption` is the built-in fallback
 * used when GIPHY is unavailable or not configured.
 */
export interface ReactionStyle {
  queries: string[];
  emoji: string;
  caption: string;
}

export const REACTIONS: Record<EventType, ReactionStyle> = {
  surprise: { queries: ['shocked', 'omg', 'surprised', 'mind blown'], emoji: '😱', caption: 'WAIT WHAT' },
  laugh: { queries: ['lol', 'laughing', 'crying laughing', 'hahaha'], emoji: '😂', caption: 'LMAO' },
  celebrate: { queries: ['celebrate', 'lets go', 'yes', 'party'], emoji: '🎉', caption: "LET'S GOOO" },
  facepalm: { queries: ['facepalm', 'smh', 'why', 'disappointed'], emoji: '🤦', caption: 'bruh.' },
  fall: { queries: ['fail', 'oops', 'wasted', 'ouch'], emoji: '💥', caption: 'WASTED' },
  jump: { queries: ['boing', 'wow', 'jumping', 'whee'], emoji: '🦘', caption: 'BOING' },
  suddenMotion: { queries: ['zoom', 'whoa', 'speed', 'woah'], emoji: '💨', caption: 'ZOOM' },
  snack: { queries: ['nom nom', 'hungry', 'yummy', 'snack'], emoji: '🍕', caption: 'snack attack' },
  phone: { queries: ['texting', 'phone', 'scrolling', 'busy'], emoji: '📱', caption: 'one sec...' },
  pet: { queries: ['cute', 'aww', 'heart eyes', 'good boy'], emoji: '🐶', caption: 'AWW' },
  enter: { queries: ['hello', 'hi', 'wave', 'its me'], emoji: '👋', caption: 'oh hey' },
  leave: { queries: ['bye', 'peace out', 'see ya', 'im out'], emoji: '✌️', caption: 'aight imma head out' },
  awkward: { queries: ['awkward', 'crickets', 'silence', 'cringe'], emoji: '🦗', caption: '*crickets*' },
  loud: { queries: ['loud', 'scream', 'boom', 'what was that'], emoji: '📢', caption: 'LOUD' },
};

export const ALLOWED_QUERIES: ReadonlySet<string> = new Set(Object.values(REACTIONS).flatMap((r) => r.queries));

export const EVENT_LABEL: Record<EventType, string> = {
  surprise: 'Surprise',
  laugh: 'Laugh',
  celebrate: 'Celebration',
  facepalm: 'Facepalm',
  fall: 'Fall',
  jump: 'Jump',
  suddenMotion: 'Sudden move',
  snack: 'Snack grab',
  phone: 'Phone check',
  pet: 'Pet spotted',
  enter: 'Entrance',
  leave: 'Exit',
  awkward: 'Awkward silence',
  loud: 'Loud noise',
};
