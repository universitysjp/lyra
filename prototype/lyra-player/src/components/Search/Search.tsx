import { useEffect, useState } from 'react';

import { useLocation, useNavigate, useSearchParams } from 'react-router-dom';

/**
 * Library-scoped search box. Queries are sent to GET /api/catalog/tracks/search, which
 * runs a PostgreSQL pg_trgm similarity match, so misspellings still find a track.
 */
const Search: React.FC = () => {
  const [params] = useSearchParams();
  const location = useLocation();
  const navigate = useNavigate();

  const urlQuery = params.get('q') ?? '';
  const [term, setTerm] = useState<string>(urlQuery);

  const onSearchPage = location.pathname === '/search';

  // Keeps the box in sync when the query changes from outside (back button, clear).
  useEffect(() => {
    setTerm(urlQuery);
  }, [urlQuery]);

  /**
   * Navigates to the search route, keeping the term in the URL so results are shareable.
   */
  const submit = (value: string): void => {
    const trimmed = value.trim();

    if (trimmed.length === 0) {
      navigate('/search');

      return;
    }

    navigate(`/search?q=${encodeURIComponent(trimmed)}`);
  };

  return (
    <nav className='search flex flex-h-center' aria-label='Search your library'>
      <form
        className='search-container'
        onSubmit={(event) => {
          event.preventDefault();
          submit(term);
        }}
      >
        <label htmlFor='search' className='flex flex-gap-medium flex-h-center flex-v-center'>
          <span className='material-symbols-outlined' aria-hidden='true'>
            search
          </span>
          <input
            type='search'
            id='search'
            name='search'
            maxLength={64}
            autoComplete='off'
            value={term}
            placeholder='Fuzzy search your synced tracks...'
            onChange={(event) => {
              setTerm(event.target.value);

              if (onSearchPage) {
                navigate(`/search?q=${encodeURIComponent(event.target.value)}`, { replace: true });
              }
            }}
          />
        </label>
      </form>
    </nav>
  );
};

export default Search;
