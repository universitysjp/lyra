import { describe, expect, it } from 'vitest';

import { formatDuration, formatRelativeTime, formatTime, hashHue, slugify } from './toolbox';

describe('formatTime', () => {
  it('pads minutes and seconds', () => {
    expect(formatTime(0)).toBe('00:00');
    expect(formatTime(65)).toBe('01:05');
    expect(formatTime(355)).toBe('05:55');
  });

  it('guards against invalid durations from unloaded media', () => {
    expect(formatTime(Number.NaN)).toBe('00:00');
    expect(formatTime(-1)).toBe('00:00');
    expect(formatTime(Number.POSITIVE_INFINITY)).toBe('00:00');
  });
});

describe('formatDuration', () => {
  it('treats missing catalog durations as zero', () => {
    expect(formatDuration(null)).toBe('00:00');
    expect(formatDuration(undefined)).toBe('00:00');
    expect(formatDuration(209)).toBe('03:29');
  });
});

describe('formatRelativeTime', () => {
  const now = new Date('2026-11-17T12:00:00Z');

  it('describes recent imports as just now', () => {
    expect(formatRelativeTime('2026-11-17T11:59:40Z', now)).toBe('just now');
  });

  it('uses singular and plural units', () => {
    expect(formatRelativeTime('2026-11-17T11:59:00Z', now)).toBe('1 minute ago');
    expect(formatRelativeTime('2026-11-17T11:30:00Z', now)).toBe('30 minutes ago');
    expect(formatRelativeTime('2026-11-17T07:00:00Z', now)).toBe('5 hours ago');
    expect(formatRelativeTime('2026-11-15T12:00:00Z', now)).toBe('2 days ago');
    expect(formatRelativeTime('2025-11-17T12:00:00Z', now)).toBe('1 year ago');
  });

  it('handles unusable timestamps', () => {
    expect(formatRelativeTime('not-a-date', now)).toBe('unknown');
  });
});

describe('slugify', () => {
  it('normalizes album names for route ids', () => {
    expect(slugify('  A Night  at the Opera! ')).toBe('a-night-at-the-opera');
    expect(slugify('***')).toBe('unknown');
    expect(slugify('Æther Worlds')).toBe('æther-worlds');
  });
});

describe('hashHue', () => {
  it('is deterministic and inside the color wheel', () => {
    expect(hashHue('Queen')).toBe(hashHue('Queen'));
    expect(hashHue('Queen')).not.toBe(hashHue('Dire Straits'));
    expect(hashHue('anything')).toBeGreaterThanOrEqual(0);
    expect(hashHue('anything')).toBeLessThan(360);
    expect(hashHue('')).toBeGreaterThanOrEqual(0);
  });
});
