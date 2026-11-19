import { describe, expect, it } from 'vitest';

import { decodeTextFrame, findId3Header, parseId3v2, tagsFromFileName } from './id3';
import { binaryFrame, buildId3Tag, textFrame, textFrameV4, u8 } from '../test/id3Fixture';

const encoder = new TextEncoder();

/** UTF-16 little-endian body with BOM, the common encoding for tagged MP3s. */
const utf16Le = (text: string): Uint8Array => {
  const bytes = [0x01, 0xff, 0xfe];

  for (const character of text) {
    const code = character.charCodeAt(0);

    bytes.push(code & 0xff, (code >> 8) & 0xff);
  }

  return Uint8Array.from(bytes);
};

/** UTF-16 big-endian without BOM (ID3v2 encoding 2). */
const utf16Be = (text: string): Uint8Array => {
  const bytes = [0x02];

  for (const character of text) {
    const code = character.charCodeAt(0);

    bytes.push((code >> 8) & 0xff, code & 0xff);
  }

  return Uint8Array.from(bytes);
};

describe('decodeTextFrame', () => {
  it('reads latin-1 and strips trailing nulls', () => {
    expect(decodeTextFrame(u8(0x00, ...encoder.encode('Queen'), 0x00))).toBe('Queen');
  });

  it('reads utf-8', () => {
    expect(decodeTextFrame(u8(0x03, ...encoder.encode('Æther')))).toBe('Æther');
  });

  it('reads utf-16 with a little-endian byte order mark', () => {
    expect(decodeTextFrame(utf16Le('Bohemian Rhapsody'))).toBe('Bohemian Rhapsody');
  });

  it('reads big-endian utf-16 without a byte order mark', () => {
    expect(decodeTextFrame(utf16Be('Sultans'))).toBe('Sultans');
  });

  it('returns nothing for unknown encodings', () => {
    expect(decodeTextFrame(u8(0x09, 0x41, 0x00))).toBe('');
  });
});

describe('parseId3v2', () => {
  it('reads title, artist, album and track from v2.3 text frames', () => {
    const bytes = buildId3Tag([
      textFrame('TIT2', 'Bohemian Rhapsody'),
      textFrame('TPE1', 'Queen'),
      textFrame('TALB', 'A Night at the Opera'),
      textFrame('TRCK', '9/12'),
    ]);

    expect(parseId3v2(bytes)).toEqual({
      title: 'Bohemian Rhapsody',
      artist: 'Queen',
      album: 'A Night at the Opera',
      trackNumber: 9,
    });
  });

  it('reads synchsafe frame sizes in v2.4', () => {
    const bytes = buildId3Tag(
      [textFrameV4('TIT2', 'Sultans of Swing'), textFrameV4('TPE1', 'Dire Straits')],
      4,
    );

    expect(parseId3v2(bytes)).toEqual({ title: 'Sultans of Swing', artist: 'Dire Straits' });
  });

  it('skips non-text frames such as embedded artwork', () => {
    const bytes = buildId3Tag([binaryFrame('APIC', 6), textFrame('TIT2', 'Take Five')]);

    expect(parseId3v2(bytes).title).toBe('Take Five');
  });

  it('ignores files without an ID3v2 header', () => {
    expect(parseId3v2(u8(...encoder.encode('RIFF')))).toEqual({});
    expect(parseId3v2(new Uint8Array(0))).toEqual({});
  });

  it('stops cleanly when a declared frame exceeds the tag', () => {
    const broken = buildId3Tag([textFrame('TIT2', 'Truncated value here')]).slice(0, 20);

    expect(parseId3v2(broken)).toEqual({});
  });
});

describe('findId3Header', () => {
  it('locates a header behind a leading chunk', () => {
    const bytes = u8(0x00, 0x00, ...buildId3Tag([textFrame('TIT2', 'Delayed')]));

    expect(findId3Header(bytes)).toBe(2);
    expect(findId3Header(u8(...encoder.encode('no tags here')))).toBe(-1);
  });
});

describe('tagsFromFileName', () => {
  it('splits "Artist - Title" names', () => {
    expect(tagsFromFileName('Queen - Bohemian Rhapsody.mp3')).toEqual({
      artist: 'Queen',
      title: 'Bohemian Rhapsody',
    });
  });

  it('falls back to the whole base name', () => {
    expect(tagsFromFileName('live_take_07.m4a')).toEqual({ title: 'live take 07' });
  });
});
