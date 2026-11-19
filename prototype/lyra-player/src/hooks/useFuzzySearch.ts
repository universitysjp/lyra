import { useEffect, useRef, useState } from 'react';

import { describeError, isAbortError } from '../api/errors';
import { toTracks } from '../api/mappers';
import useApi from './useApi';

import type { ISearchState } from '../types/types';

/** Matches the backend's own debounce expectations; the gateway rate limits at 5 req/s. */
export const SEARCH_DEBOUNCE_MS = 400;

/**
 * Debounced fuzzy search against GET /api/catalog/tracks/search, which uses PostgreSQL
 * pg_trgm similarity so typos still match.
 */
const useFuzzySearch = (query: string): ISearchState & { retry: () => void } => {
  const { catalog } = useApi();
  const [state, setState] = useState<ISearchState>({
    query: '',
    results: [],
    loading: false,
    error: null,
  });
  const [attempt, setAttempt] = useState<number>(0);
  const controller = useRef<AbortController | null>(null);

  useEffect(() => {
    const term = query.trim();

    controller.current?.abort();

    if (term.length === 0) {
      setState({ query: '', results: [], loading: false, error: null });

      return;
    }

    const active = new AbortController();

    controller.current = active;
    setState((current) => ({ ...current, query: term, loading: true, error: null }));

    const timer = window.setTimeout(async () => {
      try {
        const items = await catalog.searchTracks(term, active.signal);

        if (!active.signal.aborted) {
          setState({ query: term, results: toTracks(items), loading: false, error: null });
        }
      } catch (caught: unknown) {
        if (isAbortError(caught)) {
          return;
        }

        setState({ query: term, results: [], loading: false, error: describeError(caught) });
      }
    }, SEARCH_DEBOUNCE_MS);

    return () => {
      window.clearTimeout(timer);
      active.abort();
    };
  }, [catalog, query, attempt]);

  return { ...state, retry: () => setAttempt((value) => value + 1) };
};

export default useFuzzySearch;
