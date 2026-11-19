import type { TrackMetadataItem } from '../types/lyra';

/**
 * Ready-to-sync demo batch so the prototype can be shown without a phone full of MP3s.
 * Re-importing the same batch is how the backend dedupe (TrackFingerprint) gets demonstrated.
 */
export const SAMPLE_TRACKS: TrackMetadataItem[] = [
  {
    localTrackId: 'device-file-bohemian-rhapsody-45210034-0',
    title: 'Bohemian Rhapsody',
    artist: 'Queen',
    album: 'A Night at the Opera',
    durationSeconds: 355,
  },
  {
    localTrackId: 'device-file-sultans-of-swing-32114022-0',
    title: 'Sultans of Swing',
    artist: 'Dire Straits',
    album: 'Dire Straits',
    durationSeconds: 348,
  },
  {
    localTrackId: 'device-file-dont-stop-me-now-21560011-0',
    title: "Don't Stop Me Now",
    artist: 'Queen',
    album: 'Jazz',
    durationSeconds: 209,
  },
  {
    localTrackId: 'device-file-walking-on-the-sun-20115044-0',
    title: 'Walking on the Sun',
    artist: 'Smash Mouth',
    album: 'Astro Lounge',
    durationSeconds: 199,
  },
  {
    localTrackId: 'device-file-take-five-35012088-0',
    title: 'Take Five',
    artist: 'Dave Brubeck Quartet',
    album: 'Time Out',
    durationSeconds: 324,
  },
  {
    localTrackId: 'device-file-rogue-hunter-28840012-0',
    title: 'Rogue Hunter',
    artist: 'Night Owl Trio',
    album: null,
    durationSeconds: 262,
  },
];

export const SAMPLE_PLAYLIST_NAME = 'Local-first demo';
