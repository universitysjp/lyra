/**
 * Byte-level ID3v2 builders shared by the parser tests, the metadata-extraction tests
 * and the import flow tests, so fixtures stay honest about the tag format.
 */

const encoder = new TextEncoder();

export const u8 = (...bytes: number[]): Uint8Array => Uint8Array.from(bytes);

/** ID3v2.3 text frame: four-byte big-endian size, flags, encoding byte, then the payload. */
export const textFrame = (id: string, text: string): Uint8Array => {
  const body = u8(0x00, ...encoder.encode(text), 0x00);
  const header = [
    ...encoder.encode(id),
    (body.length >>> 24) & 0xff,
    (body.length >>> 16) & 0xff,
    (body.length >>> 8) & 0xff,
    body.length & 0xff,
    0x00,
    0x00,
  ];

  return u8(...header, ...body);
};

/** ID3v2.4 text frame, where sizes are synchsafe and utf-8 is the default encoding. */
export const textFrameV4 = (id: string, text: string): Uint8Array => {
  const body = u8(0x03, ...encoder.encode(text));
  const header = [
    ...encoder.encode(id),
    (body.length >> 21) & 0x7f,
    (body.length >> 14) & 0x7f,
    (body.length >> 7) & 0x7f,
    body.length & 0x7f,
    0x00,
    0x00,
  ];

  return u8(...header, ...body);
};

/** A picture frame the parser must skip over rather than stop at. */
export const binaryFrame = (id: string, size: number): Uint8Array =>
  u8(
    ...encoder.encode(id),
    (size >>> 24) & 0xff,
    (size >>> 16) & 0xff,
    (size >>> 8) & 0xff,
    size & 0xff,
    0x00,
    0x00,
    ...Array.from(new Uint8Array(size)),
  );

export const buildId3Tag = (frames: Uint8Array[], version: 3 | 4 = 3): Uint8Array => {
  const total = frames.reduce((sum, frame) => sum + frame.length, 0);
  const header = u8(
    0x49,
    0x44,
    0x33,
    version,
    0x00,
    0x00,
    (total >> 21) & 0x7f,
    (total >> 14) & 0x7f,
    (total >> 7) & 0x7f,
    total & 0x7f,
  );

  // Typed arrays are not flattened by Array.prototype.flat, so copy the frames by hand.
  const body = new Uint8Array(total);
  let offset = 0;

  frames.forEach((frame) => {
    body.set(frame, offset);
    offset += frame.length;
  });

  return u8(...header, ...Array.from(body));
};

/** Convenience: a tagged file header with the usual TIT2/TPE1/TALB/TRCK frames. */
export const taggedHeader = (tags: {
  title?: string;
  artist?: string;
  album?: string;
  track?: string;
}): Uint8Array => {
  const frames: Uint8Array[] = [];

  if (tags.title) {
    frames.push(textFrame('TIT2', tags.title));
  }

  if (tags.artist) {
    frames.push(textFrame('TPE1', tags.artist));
  }

  if (tags.album) {
    frames.push(textFrame('TALB', tags.album));
  }

  if (tags.track) {
    frames.push(textFrame('TRCK', tags.track));
  }

  return buildId3Tag(frames);
};
