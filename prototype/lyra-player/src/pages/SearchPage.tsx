import { Link, useSearchParams } from 'react-router-dom';

// components
import Banner from '../components/Feedback/Banner';
import MissingMediaNotice from '../components/Media/MissingMediaNotice';
import Search from '../components/Search/Search';
import TrackList from '../components/TrackList/TrackList';

// hooks
import useAttachAudio from '../hooks/useAttachAudio';
import useFuzzySearch from '../hooks/useFuzzySearch';

// utils
import { albumGroupId } from '../utils/artwork';

/**
 * Fuzzy search over the caller's own library: GET /api/catalog/tracks/search?q= runs
 * pg_trgm similarity on title and artist, ordered by score, always scoped to the user.
 */
const SearchPage: React.FC = () => {
  const [params] = useSearchParams();
  const query = params.get('q') ?? '';
  const { results, loading, error, retry } = useFuzzySearch(query);
  const attach = useAttachAudio();

  return (
    <div className='albums flex flex-column flex-gap no-select'>
      <div className='container flex flex-column flex-gap'>
        <Search />

        <section className='flex flex-column flex-gap'>
          <div className='flex flex-space-between flex-v-center'>
            <h3>{query ? `Closest matches for "${query}"` : 'Search your library'}</h3>
            <Link to='/library' className='button black'>
              Browse all
            </Link>
          </div>

          <p className='muted'>
            The backend matches on title and artist with a similarity threshold of 0.3, so a typo
            like <em>bohemain rhapsody</em> still finds <em>Bohemian Rhapsody</em>.
          </p>

          <MissingMediaNotice />
          <Banner message={error} onRetry={retry} />

          {loading ? (
            <p className='empty'>Searching...</p>
          ) : (
            <TrackList
              tracks={results}
              onAttach={(track) => void attach(track, results)}
              albumFor={(track) => albumGroupId(track.artist, track.album)}
              emptyLabel={
                query
                  ? 'No close matches in your library.'
                  : 'Type something above to search the tracks you synced.'
              }
            />
          )}
        </section>
      </div>
    </div>
  );
};

export default SearchPage;
