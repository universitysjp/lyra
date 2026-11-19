import { hasUsableToken, type Session, type SessionStore } from './session';
import type { AuthResponse } from '../types/lyra';

/** RFC 9110 problem details as emitted by the Lyra exception middleware. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  detail?: string;
  status?: number;
}

const STATUS_MESSAGES: Record<number, string> = {
  400: 'The request was rejected.',
  401: 'You are not signed in.',
  403: 'You are not allowed to do that.',
  404: 'Not found.',
  409: 'That resource already exists.',
  423: 'Too many failed attempts, try again later.',
  429: 'Rate limited by the gateway, slow down.',
  500: 'The backend failed to handle the request.',
  502: 'The gateway could not reach the service.',
  503: 'The backend is unavailable.',
};

export class ApiError extends Error {
  readonly status: number;
  readonly title?: string;
  readonly detail?: string;

  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.title = problem?.title;
    this.detail = problem?.detail;
  }
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  body?: unknown;
  /** Attach the bearer token, default true. */
  auth?: boolean;
  signal?: AbortSignal;
  /** Force the request out even when the access token looks expired. */
  skipPreRefresh?: boolean;
}

export interface LyraClientOptions {
  store: SessionStore;
  baseUrl?: string;
  fetchImpl?: typeof fetch;
  onSessionLost?: () => void;
}

export interface LyraClient {
  session(): Session | null;
  saveSession(session: Session): void;
  clearSession(): void;
  /** Rotates the refresh token; resolves null when the session is gone. */
  refresh(): Promise<Session | null>;
  request<T>(path: string, options?: RequestOptions): Promise<T>;
}

const readProblem = async (response: Response): Promise<ProblemDetails | null> => {
  const contentType = response.headers.get('content-type') ?? '';

  if (!contentType.includes('json')) {
    return null;
  }

  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    return null;
  }
};

const toError = (response: Response, problem: ProblemDetails | null): ApiError => {
  const message =
    problem?.detail?.trim() ||
    problem?.title?.trim() ||
    STATUS_MESSAGES[response.status] ||
    `Request failed with status ${response.status}.`;

  return new ApiError(response.status, message, problem ?? undefined);
};

/**
 * Thin fetch wrapper for the Lyra gateway. Adds bearer tokens, refreshes an expired access
 * token once per request through POST /api/auth/refresh, and surfaces problem+json errors.
 */
export const createLyraClient = (options: LyraClientOptions): LyraClient => {
  const { store, baseUrl = '', onSessionLost } = options;
  const fetchImpl: typeof fetch = (...args) =>
    (options.fetchImpl ?? globalThis.fetch)(...args);

  let current = store.load();
  let inflight: Promise<Session | null> | null = null;

  const persist = (session: Session | null): void => {
    current = session;

    if (session) {
      store.save(session);
    } else {
      store.clear();
    }
  };

  const send = async <T>(
    path: string,
    init: RequestInit & { headers: Record<string, string> },
    expectJson = true,
  ): Promise<T> => {
    const response = await fetchImpl(`${baseUrl}${path}`, init);

    if (!response.ok) {
      throw toError(response, await readProblem(response));
    }

    if (!expectJson || response.status === 204) {
      return undefined as T;
    }

    const contentType = response.headers.get('content-type') ?? '';

    if (!contentType.includes('json')) {
      return (await response.text()) as T;
    }

    return (await response.json()) as T;
  };

  const buildHeaders = (
    session: Session | null,
    options: { body?: unknown; auth?: boolean },
  ): Record<string, string> => {
    const headers: Record<string, string> = { Accept: 'application/json' };

    if (options.body !== undefined) {
      headers['Content-Type'] = 'application/json';
    }

    if (options.auth !== false && session?.accessToken) {
      headers.Authorization = `Bearer ${session.accessToken}`;
    }

    return headers;
  };

  const doRefresh = async (): Promise<Session | null> => {
    const refreshToken = current?.refreshToken;

    if (!refreshToken) {
      return null;
    }

    try {
      const issued = await send<AuthResponse>('/api/auth/refresh', {
        method: 'POST',
        headers: buildHeaders(null, { body: { refreshToken } }),
        body: JSON.stringify({ refreshToken }),
      });

      const session: Session = {
        userId: issued.userId,
        email: current?.email ?? '',
        accessToken: issued.accessToken,
        refreshToken: issued.refreshToken,
        expiresAt: Date.now() + issued.expiresInSeconds * 1000,
      };

      persist(session);

      return session;
    } catch {
      persist(null);
      onSessionLost?.();

      return null;
    }
  };

  const refresh = (): Promise<Session | null> => {
    inflight ??= doRefresh().finally(() => {
      inflight = null;
    });

    return inflight;
  };

  const request = async <T>(path: string, requestOptions: RequestOptions = {}): Promise<T> => {
    const { auth = true, body, method = 'GET', signal, skipPreRefresh } = requestOptions;

    if (auth && !skipPreRefresh && current && !hasUsableToken(current) && current.refreshToken) {
      await refresh();
    }

    const execute = (): Promise<T> =>
      send<T>(
        path,
        {
          method,
          headers: buildHeaders(current, { body, auth }),
          body: body === undefined ? undefined : JSON.stringify(body),
          signal,
        },
        true,
      );

    try {
      return await execute();
    } catch (error) {
      if (error instanceof ApiError && error.status === 401 && auth && current?.refreshToken) {
        const rotated = await refresh();

        if (rotated) {
          return execute();
        }
      }

      throw error;
    }
  };

  return {
    session: () => current,
    saveSession: (session) => persist(session),
    clearSession: () => {
      persist(null);
      onSessionLost?.();
    },
    refresh,
    request,
  };
};
