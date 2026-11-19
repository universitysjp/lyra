/**
 * Formats a given duration in seconds into a string in the format mm:ss.
 *
 * Kept from the upstream template (react-music-player, MIT).
 *
 * @param {number} duration - The duration in seconds to be formatted.
 * @returns {string} The formatted time string in mm:ss format.
 */
export const formatTime = (duration: number): string => {
  if (!Number.isFinite(duration) || duration < 0) {
    return '00:00';
  }

  const min = Math.floor(duration / 60);
  const sec = Math.floor(duration - min * 60);

  return [min, sec].map((n) => (n < 10 ? `0${n}` : n)).join(':');
};

/** Formats a seconds value returned by the catalog API, tolerating null-ish input. */
export const formatDuration = (seconds: number | null | undefined): string =>
  formatTime(typeof seconds === 'number' ? seconds : 0);

/** Renders an ISO timestamp as a short relative label, e.g. `just now`, `4 h ago`. */
export const formatRelativeTime = (iso: string, now: Date = new Date()): string => {
  const then = new Date(iso);

  if (Number.isNaN(then.getTime())) {
    return 'unknown';
  }

  const seconds = Math.max(0, Math.floor((now.getTime() - then.getTime()) / 1000));

  if (seconds < 60) {
    return 'just now';
  }

  const steps: [number, number, string][] = [
    [3600, 60, 'minute'],
    [86400, 3600, 'hour'],
    [2592000, 86400, 'day'],
    [31536000, 2592000, 'month'],
  ];

  for (const [limit, divisor, label] of steps) {
    if (seconds < limit) {
      const value = Math.floor(seconds / divisor);

      return `${value} ${value === 1 ? label : `${label}s`} ago`;
    }
  }

  const years = Math.floor(seconds / 31536000);

  return `${years} ${years === 1 ? 'year' : 'years'} ago`;
};

/** Normalizes text into a stable key, used for grouping library tracks by album. */
export const slugify = (value: string): string =>
  value
    .trim()
    .toLowerCase()
    .replace(/[^\p{L}\p{N}]+/gu, '-')
    .replace(/^-+|-+$/g, '') || 'unknown';

/** Deterministic 0-359 hue for placeholder artwork, derived from any string. */
export const hashHue = (value: string): number => {
  let hash = 2166136261;

  for (let index = 0; index < value.length; index += 1) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }

  return Math.abs(hash) % 360;
};
