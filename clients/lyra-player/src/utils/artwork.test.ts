import { describe, expect, it } from 'vite-plus/test';

import { albumGroupId, artworkFor } from './artwork';

describe('artworkFor', () => {
  it('produces a self-contained svg data uri', () => {
    const uri = artworkFor('Queen::A Night at the Opera', 'A Night at the Opera');

    expect(uri.startsWith('data:image/svg+xml;charset=utf-8,')).toBe(true);
    expect(decodeURIComponent(uri)).toContain('AN');
    // The only URL in the artwork is the SVG namespace: no external image hosts.
    expect(uri).not.toContain('https:');
    expect(decodeURIComponent(uri)).not.toContain('src=');
  });

  it('is deterministic per seed and differs across seeds', () => {
    expect(artworkFor('Dire Straits', 'Dire Straits')).toBe(
      artworkFor('Dire Straits', 'Dire Straits'),
    );
    expect(artworkFor('Queen', 'Queen')).not.toBe(artworkFor('Adele', 'Adele'));
  });

  it('handles empty seeds without breaking the label', () => {
    expect(artworkFor('')).toContain('Lyra');
  });
});

describe('albumGroupId', () => {
  it('builds a route-safe id from artist and album', () => {
    expect(albumGroupId('Queen', 'A Night at the Opera')).toBe('queen/a-night-at-the-opera');
    expect(albumGroupId('  ', '')).toBe('unknown/unknown');
  });
});
