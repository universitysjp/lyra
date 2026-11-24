/** Persisted login state. Access tokens are short lived (15 min) and rotate on refresh. */
export interface Session {
  userId: string;
  email: string;
  accessToken: string;
  refreshToken: string;
  /** Epoch milliseconds when the access token expires. */
  expiresAt: number;
}

export interface SessionStore {
  load(): Session | null;
  save(session: Session): void;
  clear(): void;
}

const STORAGE_KEY = 'lyra.session';

/** localStorage-backed store used by the browser build. */
export const createSessionStore = (
  storage: Pick<Storage, 'getItem' | 'setItem' | 'removeItem'> | null = globalThis.localStorage ?? null,
): SessionStore => {
  if (!storage) {
    return createMemorySessionStore();
  }

  return {
    load() {
      try {
        const raw = storage.getItem(STORAGE_KEY);

        return raw ? (JSON.parse(raw) as Session) : null;
      } catch {
        return null;
      }
    },
    save(session) {
      storage.setItem(STORAGE_KEY, JSON.stringify(session));
    },
    clear() {
      storage.removeItem(STORAGE_KEY);
    },
  };
};

/** In-memory store for tests and SSR-safe defaults. */
export const createMemorySessionStore = (): SessionStore => {
  let current: Session | null = null;

  return {
    load: () => current,
    save: (session) => {
      current = session;
    },
    clear: () => {
      current = null;
    },
  };
};

export const hasUsableToken = (session: Session | null, skewMs = 15_000): boolean =>
  Boolean(session?.accessToken) && (session?.expiresAt ?? 0) - skewMs > Date.now();
