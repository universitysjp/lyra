import { useEffect, useState } from 'react';

import { useLocation, useNavigate, useSearchParams } from 'react-router-dom';

/** Global library search. Keeps the query in the URL so results are shareable. */
const Search: React.FC = () => {
  const [params] = useSearchParams();
  const location = useLocation();
  const navigate = useNavigate();

  const urlQuery = params.get('q') ?? '';
  const [term, setTerm] = useState<string>(urlQuery);

  const onSearchPage = location.pathname === '/search';

  useEffect(() => {
    setTerm(urlQuery);
  }, [urlQuery]);

  const submit = (value: string): void => {
    const trimmed = value.trim();

    if (trimmed.length === 0) {
      navigate('/search');

      return;
    }

    navigate(`/search?q=${encodeURIComponent(trimmed)}`);
  };

  const clear = (): void => {
    setTerm('');
    navigate('/search');
  };

  return (
    <form
      className='global-search'
      role='search'
      aria-label='Search your library'
      onSubmit={(event) => {
        event.preventDefault();
        submit(term);
      }}
    >
      <span className='material-symbols-outlined' aria-hidden='true'>
        search
      </span>
      <input
        type='search'
        id='search'
        name='search'
        maxLength={64}
        autoComplete='off'
        aria-label='Search songs, artists and albums'
        value={term}
        placeholder='Search songs, artists, albums'
        onChange={(event) => {
          setTerm(event.target.value);

          if (onSearchPage) {
            const next = event.target.value.trim();
            navigate(next ? `/search?q=${encodeURIComponent(next)}` : '/search', { replace: true });
          }
        }}
      />
      {term.length > 0 && (
        <button type='button' className='search-clear' onClick={clear} aria-label='Clear search'>
          <span className='material-symbols-outlined' aria-hidden='true'>
            close
          </span>
        </button>
      )}
    </form>
  );
};

export default Search;
