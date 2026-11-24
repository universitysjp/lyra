export interface LyricLine {
  /** Seconds into the track, or null for unsynced plain-text lines. */
  timeSeconds: number | null;
  text: string;
}

const TIMESTAMP = /\[(\d{1,3}):(\d{2})(?:[.:](\d{1,3}))?\]/g;

const toSeconds = (minutes: string, seconds: string, fraction?: string): number => {
  const fractionSeconds = fraction ? Number(`0.${fraction.padEnd(3, '0').slice(0, 3)}`) : 0;

  return Number(minutes) * 60 + Number(seconds) + fractionSeconds;
};

/**
 * Parses LRC lines such as `[00:12.34]Text`. Metadata tags like `[ar:Artist]`
 * have no numeric timestamp and are ignored. Multiple timestamps on one line
 * produce multiple entries, matching the LRC format.
 */
export const parseSyncedLyrics = (value: string | null | undefined): LyricLine[] => {
  if (!value) {
    return [];
  }

  const lines: LyricLine[] = [];

  value.split(/\r?\n/).forEach((raw) => {
    const timestamps = [...raw.matchAll(TIMESTAMP)];

    if (timestamps.length === 0) {
      return;
    }

    const text = raw.replace(TIMESTAMP, '').trim();

    if (!text) {
      return;
    }

    timestamps.forEach((timestamp) => {
      lines.push({
        timeSeconds: toSeconds(timestamp[1] ?? '0', timestamp[2] ?? '0', timestamp[3]),
        text,
      });
    });
  });

  return lines.sort((left, right) => (left.timeSeconds ?? 0) - (right.timeSeconds ?? 0));
};

/** Splits plain lyrics into non-empty display lines. */
export const splitPlainLyrics = (value: string | null | undefined): LyricLine[] => {
  if (!value) {
    return [];
  }

  return value
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0)
    .map((text) => ({ timeSeconds: null, text }));
};

/** Returns the index of the last line at or before `position`, or -1. */
export const activeLyricIndex = (lines: LyricLine[], position: number): number => {
  let active = -1;

  for (let index = 0; index < lines.length; index += 1) {
    const time = lines[index]?.timeSeconds;

    if (time === null || time === undefined || time > position) {
      break;
    }

    active = index;
  }

  return active;
};
