import { hashHue, slugify } from './toolbox';

/**
 * Lyra syncs metadata only, so there is no cover art in the catalog. The template UI expects
 * image URLs, therefore we generate deterministic inline SVG artwork from the album/artist name.
 * No network requests, no external images.
 */
export const artworkFor = (seed: string, initials?: string): string => {
  const hue = hashHue(seed || 'lyra');
  const label = (initials ?? seed)
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => word[0]?.toUpperCase() ?? '')
    .join('');

  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 300 300" role="img">
<defs>
<linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
<stop offset="0" stop-color="hsl(${hue}, 62%, 34%)"/>
<stop offset="1" stop-color="hsl(${(hue + 48) % 360}, 72%, 12%)"/>
</linearGradient>
</defs>
<rect width="300" height="300" fill="url(#g)"/>
<circle cx="238" cy="62" r="46" fill="hsla(0,0%,100%,0.08)"/>
<text x="150" y="172" font-family="'Open Sans',sans-serif" font-size="96" font-weight="700"
fill="rgba(255,255,255,0.86)" text-anchor="middle">${label.slice(0, 3) || 'Lyra'}</text>
</svg>`;

  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
};

/** Stable id for an album grouping so it survives refetches and can be used in the router. */
export const albumGroupId = (artist: string, album: string): string =>
  `${slugify(artist)}/${slugify(album)}`;
