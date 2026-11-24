import { albumGroupId, artworkFor } from '../utils/artwork';
import { localAudioUrl } from '../utils/localAudioStore';
import { formatDuration } from '../utils/toolbox';
import type { LocalTrack } from '../utils/localFiles';
import type { TrackDto, TrackMetadataItem } from '../types/lyra';
import type { IAlbumGroup, ITrack } from '../types/types';

const SINGLES_LABEL = 'Singles';

/** Maps a catalog DTO onto the shape the player components render. */
export const toTrack = (dto: TrackDto, no: number): ITrack => {
  const album = dto.album?.trim() || SINGLES_LABEL;

  return {
    id: dto.id,
    localTrackId: dto.localTrackId,
    no,
    name: dto.title,
    artist: dto.artist,
    album,
    duration: formatDuration(dto.durationSeconds),
    durationSeconds: dto.durationSeconds,
    isEnriched: dto.isEnriched,
    hasLyrics: dto.hasLyrics,
    hasSyncedLyrics: dto.hasSyncedLyrics,
    importedAt: dto.importedAt,
    mediaurl: localAudioUrl(dto.localTrackId),
    artwork: artworkFor(`${dto.artist}::${album}`, album === SINGLES_LABEL ? dto.artist : album),
  };
};

export const toTracks = (dtos: TrackDto[]): ITrack[] =>
  dtos.map((dto, index) => toTrack(dto, index + 1));

/** Re-reads the local audio registry, e.g. after the user attaches a file. */
export const withLocalAudio = (tracks: ITrack[]): ITrack[] =>
  tracks.map((track) => {
    const url = localAudioUrl(track.localTrackId);

    return url === track.mediaurl ? track : { ...track, mediaurl: url };
  });

/** Groups a flat library by artist + album, mirroring the template's album views. */
export const groupByAlbum = (tracks: ITrack[]): IAlbumGroup[] => {
  const groups = new Map<string, IAlbumGroup>();

  tracks.forEach((track) => {
    const id = albumGroupId(track.artist, track.album);
    const existing = groups.get(id);

    if (existing) {
      existing.tracks.push(track);
      existing.songs = existing.tracks.length;
      existing.minutes = Math.round(
        existing.tracks.reduce((total, item) => total + item.durationSeconds, 0) / 60,
      );

      return;
    }

    groups.set(id, {
      id,
      name: track.album,
      artist: track.artist,
      songs: 1,
      minutes: Math.round(track.durationSeconds / 60),
      image: track.artwork,
      tracks: [track],
    });
  });

  return [...groups.values()].sort((left, right) => left.artist.localeCompare(right.artist));
};

/** Builds the POST /api/catalog/tracks/batch payload from locally extracted metadata. */
export const toMetadataItems = (tracks: LocalTrack[]): TrackMetadataItem[] =>
  tracks.map((track) => ({
    localTrackId: track.localTrackId,
    title: track.title,
    artist: track.artist,
    album: track.album,
    durationSeconds: track.durationSeconds,
  }));

export const totalSeconds = (tracks: ITrack[]): number =>
  tracks.reduce((total, track) => total + (track.durationSeconds || 0), 0);
