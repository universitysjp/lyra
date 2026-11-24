import { describe, expect, it } from 'vitest';

import { activeLyricIndex, parseSyncedLyrics, splitPlainLyrics } from './lyrics';

describe('parseSyncedLyrics', () => {
  it('parses timestamps, metadata tags and repeated timestamps', () => {
    const lines = parseSyncedLyrics(
      [
        '[ar:Queen]',
        '[00:12.34]First line',
        '[00:20.00][00:40.00]Repeated line',
        '[01:02.5]Last line',
      ].join('\n'),
    );

    expect(lines).toEqual([
      { timeSeconds: 12.34, text: 'First line' },
      { timeSeconds: 20, text: 'Repeated line' },
      { timeSeconds: 40, text: 'Repeated line' },
      { timeSeconds: 62.5, text: 'Last line' },
    ]);
  });

  it('returns an empty list for missing or unsynced text', () => {
    expect(parseSyncedLyrics(null)).toEqual([]);
    expect(parseSyncedLyrics('just a plain line')).toEqual([]);
  });
});

describe('splitPlainLyrics', () => {
  it('trims and drops blank lines', () => {
    expect(splitPlainLyrics('one\n\n two \r\n')).toEqual([
      { timeSeconds: null, text: 'one' },
      { timeSeconds: null, text: 'two' },
    ]);
  });
});

describe('activeLyricIndex', () => {
  const lines = parseSyncedLyrics('[00:10.00]one\n[00:20.00]two\n[00:30.00]three');

  it('returns the last line at or before the position', () => {
    expect(activeLyricIndex(lines, 0)).toBe(-1);
    expect(activeLyricIndex(lines, 10)).toBe(0);
    expect(activeLyricIndex(lines, 25)).toBe(1);
    expect(activeLyricIndex(lines, 300)).toBe(2);
  });
});
