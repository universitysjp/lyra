import { useSearchParams } from 'react-router-dom';

import Banner from '../components/Feedback/Banner';
import MissingMediaNotice from '../components/Media/MissingMediaNotice';
import Search from '../components/Search/Search';
import TrackList from '../components/TrackList/TrackList';

import useAttachAudio from '../hooks/useAttachAudio';
import useFuzzySearch from '../hooks/useFuzzySearch';

import { albumGroupId } from '../utils/artwork';

/** Search across the caller's own library. */
const SearchPage: React.FC = () => {
  const [params] = useSearchParams();
  const query = params.get('q') ?? '';
  const { results, loading, error, retry } = useFuzzySearch(query);
  const attach = useAttachAudio();

  return (
    <div className='page search-page no-select'>
      <header className='page-head'>
        <div>
          <h1 className='page-title'>Search</h1>
          <p className='page-sub'>Find a song, an artist or a record in your library.</p>
        </div>
      </header>

      <Search />

      <MissingMediaNotice />
      <Banner message={error} onRetry={retry} />

      {!query.trim() ? (
        <div className='empty-state'>
          <span className='material-symbols-outlined' aria-hidden='true'>
            search
          </span>
          <h2>What do you want to hear?</h2>
          <p>Search the music you have already added. Titles and artists both work.</p>
        </div>
      ) : loading ? (
        <div className='skeleton-list' aria-label='Searching'>
          {[0, 1, 2].map((row) => (
            <div key={row} className='skeleton-row' />
          ))}
        </div>
      ) : results.length === 0 ? (
        <div className='empty-state'>
          <span className='material-symbols-outlined' aria-hidden='true'>
            search_off
          </span>
          <h2>No matches for “{query}”</h2>
          <p>Try a different spelling, the artist name, or another part of the title.</p>
        </div>
      ) : (
        <>
          <p className='page-sub'>
            {results.length} {results.length === 1 ? 'result' : 'results'} for “{query}”
          </p>
          <TrackList
            tracks={results}
            onAttach={(track) => void attach(track, results)}
            albumFor={(track) => albumGroupId(track.artist, track.album)}
          />
        </>
      )}
    </div>
  );
};

export default SearchPage;
