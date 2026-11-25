import { useEffect, useMemo, useRef, useState } from 'react';

import Artwork from '../components/Artwork/Artwork';
import Banner from '../components/Feedback/Banner';
import MissingMediaNotice from '../components/Media/MissingMediaNotice';
import TrackList from '../components/TrackList/TrackList';

import useApi from '../hooks/useApi';
import useAttachAudio from '../hooks/useAttachAudio';
import useTrack from '../hooks/useTrack';

import { describeError, isAbortError } from '../api/errors';
import { playlistToTracks, totalSeconds } from '../api/mappers';

import type { PlaylistDto } from '../types/lyra';

const LIMITS = [10, 20, 30, 50] as const;

/** Creates a playlist from a natural-language prompt using only the listener's library. */
const PlaylistPage: React.FC = () => {
  const { playlists } = useApi();
  const { currentTrack, currentState, playTrack, toggleTrack } = useTrack();
  const attach = useAttachAudio();

  const [prompt, setPrompt] = useState<string>('');
  const [limit, setLimit] = useState<number>(20);
  const [result, setResult] = useState<PlaylistDto | null>(null);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const inFlight = useRef<AbortController | null>(null);

  useEffect(
    () => () => {
      inFlight.current?.abort();
    },
    [],
  );

  const playlistTracks = useMemo(
    () => (result ? playlistToTracks(result.tracks) : []),
    [result],
  );

  const minutes = Math.round(totalSeconds(playlistTracks) / 60);
  const durationLabel = minutes === 0 ? 'under a minute' : `${minutes} min`;
  const isPlaying =
    currentState === 'playing' &&
    playlistTracks.some((track) => track.id === currentTrack?.id);

  const generate = async (event: React.FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();

    const text = prompt.trim();

    if (!text) {
      setError('Describe the playlist you want first.');
      return;
    }

    inFlight.current?.abort();
    const controller = new AbortController();
    inFlight.current = controller;

    setLoading(true);
    setError(null);
    setResult(null);

    try {
      const playlist = await playlists.generate({ prompt: text, limit }, controller.signal);

      if (!controller.signal.aborted) {
        setResult(playlist);
      }
    } catch (caught: unknown) {
      if (!controller.signal.aborted && !isAbortError(caught)) {
        setError(describeError(caught));
      }
    } finally {
      if (inFlight.current === controller) {
        inFlight.current = null;

        if (!controller.signal.aborted) {
          setLoading(false);
        }
      }
    }
  };

  const play = (): void => {
    if (playlistTracks.length === 0) {
      return;
    }

    if (isPlaying && currentTrack) {
      toggleTrack(currentTrack, playlistTracks);
      return;
    }

    const first = playlistTracks.find((track) => Boolean(track.mediaurl)) ?? playlistTracks[0];

    if (first) {
      playTrack(first, playlistTracks);
    }
  };

  const reasonFor = (localTrackId: string): string | null =>
    result?.tracks.find((track) => track.localTrackId === localTrackId)?.reason ?? null;

  return (
    <div className='page playlist-page no-select'>
      <header className='page-head'>
        <div>
          <h1 className='page-title'>Make a playlist</h1>
          <p className='page-sub'>
            Describe a feeling, a moment or a kind of sound. Lyra looks only at the music you own.
          </p>
        </div>
      </header>

      <section className='playlist-builder' aria-label='Playlist builder'>
        <form className='playlist-form' onSubmit={(event) => void generate(event)}>
          <label className='field playlist-prompt'>
            <span>What do you want to hear?</span>
            <textarea
              name='prompt'
              value={prompt}
              rows={3}
              maxLength={2000}
              placeholder='Late-night drive through the city, calm but not sleepy.'
              onChange={(event) => setPrompt(event.target.value)}
              disabled={loading}
            />
          </label>

          <div className='playlist-form-foot'>
            <label className='field playlist-limit'>
              <span>Tracks</span>
              <select
                value={limit}
                onChange={(event) => setLimit(Number(event.target.value))}
                disabled={loading}
              >
                {LIMITS.map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </select>
            </label>

            <button type='submit' className='btn btn-primary' disabled={loading}>
              <span className='material-symbols-outlined' aria-hidden='true'>
                auto_awesome
              </span>
              {loading ? 'Building…' : 'Build playlist'}
            </button>
          </div>
        </form>

        <div className='playlist-prompts' aria-label='Prompt ideas'>
          {[
            'Warm Sunday morning',
            'Songs for a long walk',
            'Late-night drive',
            'Something to focus to',
          ].map((idea) => (
            <button
              key={idea}
              type='button'
              className='chip'
              onClick={() => setPrompt(idea)}
              disabled={loading}
            >
              {idea}
            </button>
          ))}
        </div>
      </section>

      <Banner message={error} onDismiss={() => setError(null)} />
      <MissingMediaNotice />

      {loading && (
        <div className='playlist-loading' role='status' aria-live='polite'>
          <span className='material-symbols-outlined' aria-hidden='true'>
            graphic_eq
          </span>
          <div>
            <strong>Shaping your playlist…</strong>
            <p>Your prompt is becoming a search, then a sequence. This can take a moment.</p>
          </div>
        </div>
      )}

      {!loading && !result && (
        <div className='empty-state'>
          <span className='material-symbols-outlined' aria-hidden='true'>
            queue_music
          </span>
          <h2>Start with a feeling</h2>
          <p>
            A good prompt names a mood, an activity or a kind of energy. Lyra turns it into a
            playlist from your own catalogue — no audio leaves this device.
          </p>
        </div>
      )}

      {!loading && result && (
        <section className='playlist-result'>
          <header className='playlist-hero'>
            <Artwork
              className='playlist-cover'
              seed={`${result.title}::${result.description}`}
              initials={result.title}
              alt={`${result.title} artwork`}
            />

            <div className='playlist-hero-info'>
              <span className='album-kicker'>Generated playlist</span>
              <h2 className='playlist-title'>{result.title}</h2>
              <p className='playlist-description'>{result.description}</p>
              <div className='album-meta'>
                <span>
                  {result.tracks.length} {result.tracks.length === 1 ? 'track' : 'tracks'}
                </span>
                <span className='dot' aria-hidden='true' />
                <span>{durationLabel}</span>
              </div>

              <div className='album-actions'>
                <button
                  type='button'
                  className='btn btn-primary'
                  onClick={play}
                  disabled={playlistTracks.length === 0}
                >
                  <span className='material-symbols-outlined' aria-hidden='true'>
                    {isPlaying ? 'pause' : 'play_arrow'}
                  </span>
                  {isPlaying ? 'Pause' : 'Play'}
                </button>
                <button
                  type='button'
                  className='btn btn-ghost'
                  onClick={() => {
                    setResult(null);
                    setError(null);
                    setPrompt('');
                  }}
                >
                  <span className='material-symbols-outlined' aria-hidden='true'>
                    refresh
                  </span>
                  Start over
                </button>
              </div>
            </div>
          </header>

          {playlistTracks.length === 0 ? (
            <div className='empty-state compact'>
              <span className='material-symbols-outlined' aria-hidden='true'>
                music_off
              </span>
              <h2>Nothing fit that request</h2>
              <p>Try a broader mood, a different activity, or fewer constraints.</p>
            </div>
          ) : (
            <TrackList
              tracks={playlistTracks}
              showAlbum={false}
              onAttach={(track) => void attach(track, playlistTracks)}
              reasonFor={(track) => reasonFor(track.localTrackId)}
            />
          )}
        </section>
      )}
    </div>
  );
};

export default PlaylistPage;
