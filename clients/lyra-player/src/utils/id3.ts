/**
 * Minimal ID3v2 tag reader. The Lyra design keeps audio on the device and only uploads
 * metadata, so the prototype has to extract tags itself - exactly what the Flutter client
 * does natively. Supports ID3v2.3/2.4 text frames (TIT2, TPE1, TALB, TRCK) without any
 * third-party dependency.
 */

export interface Id3Tags {
  title?: string;
  artist?: string;
  album?: string;
  trackNumber?: number;
}

const ID3_MAGIC = [0x49, 0x44, 0x33]; // "ID3"
const MAX_HEADER_SCAN = 1024;

/** Decodes one ID3v2 text frame body, honouring the encoding byte. */
export const decodeTextFrame = (bytes: Uint8Array): string => {
  const encoding = bytes[0];
  const body = bytes.subarray(1);
  const trim = (value: string): string => value.replace(/\0+$/g, '').trim();

  if (encoding === 0) {
    return trim(new TextDecoder('iso-8859-1').decode(body));
  }

  if (encoding === 3) {
    return trim(new TextDecoder('utf-8').decode(body));
  }

  if (encoding === 1 || encoding === 2) {
    // Encoding 1 is UTF-16 with BOM, encoding 2 is UTF-16BE without BOM.
    const bomIsLittle = body[0] === 0xff && body[1] === 0xfe;
    const bomIsBig = body[0] === 0xfe && body[1] === 0xff;
    const littleEndian = encoding === 1 ? bomIsLittle || !bomIsBig : false;
    const start = encoding === 1 && (bomIsLittle || bomIsBig) ? 2 : 0;
    const view = new DataView(body.buffer, body.byteOffset + start, body.byteLength - start);
    const units: number[] = [];

    for (let index = 0; index + 1 < view.byteLength; index += 2) {
      units.push(view.getUint16(index, littleEndian));
    }

    return trim(String.fromCharCode(...units));
  }

  return '';
};

const readSynchsafe = (bytes: Uint8Array): number =>
  ((bytes[0] & 0x7f) << 21) |
  ((bytes[1] & 0x7f) << 14) |
  ((bytes[2] & 0x7f) << 7) |
  (bytes[3] & 0x7f);

const readUInt32 = (bytes: Uint8Array): number =>
  (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];

/**
 * Parses ID3v2 text frames out of the beginning of an audio file.
 * Returns undefined fields when the file has no usable tag.
 */
export const parseId3v2 = (bytes: Uint8Array): Id3Tags => {
  const tags: Id3Tags = {};

  if (bytes.length < 10 || !ID3_MAGIC.every((code, index) => bytes[index] === code)) {
    return tags;
  }

  const version = bytes[3];

  if (version < 2) {
    return tags;
  }

  const tagSize = readSynchsafe(bytes.subarray(6, 10));
  const end = Math.min(bytes.length, 10 + tagSize);
  let offset = 10;

  // Skip an extended header when present.
  if (bytes[5] & 0x40) {
    const extendedSize =
      version >= 4 ? readSynchsafe(bytes.subarray(10, 14)) : readUInt32(bytes.subarray(10, 14));
    offset = 10 + extendedSize;
  }

  while (offset + 10 <= end) {
    const id = String.fromCharCode(
      bytes[offset],
      bytes[offset + 1],
      bytes[offset + 2],
      bytes[offset + 3],
    );
    const size =
      version >= 4
        ? readSynchsafe(bytes.subarray(offset + 4, offset + 8))
        : readUInt32(bytes.subarray(offset + 4, offset + 8));

    if (size <= 0 || offset + 10 + size > end) {
      break;
    }

    // Images, lyrics and other non-text frames are skipped, not treated as the end of the tag.
    if (/^T[A-Z0-9]{3}$/.test(id)) {
      const value = decodeTextFrame(bytes.subarray(offset + 10, offset + 10 + size));

      if (id === 'TIT2') {
        tags.title = value || tags.title;
      } else if (id === 'TPE1') {
        tags.artist = value || tags.artist;
      } else if (id === 'TALB') {
        tags.album = value || tags.album;
      } else if (id === 'TRCK') {
        const parsed = Number.parseInt(value.split('/')[0] ?? '', 10);

        if (Number.isFinite(parsed)) {
          tags.trackNumber = parsed;
        }
      }
    }

    offset += 10 + size;
  }

  return tags;
};

/** Fallback metadata derived from a `Artist - Title.mp3` style file name. */
export const tagsFromFileName = (fileName: string): Id3Tags => {
  const base = fileName
    .replace(/\.[^./\\]+$/, '')
    .replace(/_/g, ' ')
    .trim();
  const [first, second] = base.split(/\s+-\s+/);

  if (first && second) {
    return { artist: first.trim(), title: second.trim() };
  }

  return { title: base };
};

/** Scans a buffer for an ID3v2 header that may be preceded by an ID3v1 or FLAC/AIFF prefix. */
export const findId3Header = (bytes: Uint8Array): number => {
  const limit = Math.min(bytes.length - 3, MAX_HEADER_SCAN);

  for (let index = 0; index <= limit; index += 1) {
    if (ID3_MAGIC.every((code, position) => bytes[index + position] === code)) {
      return index;
    }
  }

  return -1;
};
