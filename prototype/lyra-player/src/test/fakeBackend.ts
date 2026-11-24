import type {
  AiConnectionDto,
  AiProvider,
  OpenCodePlan,
  PagedResponse,
  TrackDto,
  TrackLyricsDto,
  TrackMetadataItem,
} from '../types/lyra';

/** Minimal in-memory stand-in for the Lyra gateway, used by the component and hook tests. */

interface StoredUser {
  userId: string;
  email: string;
  password: string;
  createdAt: string;
}

interface TokenPair {
  accessToken: string;
  refreshToken: string;
  userId: string;
  expiresAt: number;
}

interface StoredAiConnection {
  provider: AiProvider;
  model: string;
  apiKey: string;
  openCodePlan: OpenCodePlan | null;
  createdAt: string;
  updatedAt: string;
}

const AI_PROVIDERS: AiProvider[] = ['OpenAI', 'Gemini', 'OpenCode', 'Anthropic'];
const OPEN_CODE_PLANS: OpenCodePlan[] = ['PayAsYouGo', 'Go'];

export interface FakeBackendOptions {
  /** Seed a library for the default demo user. */
  tracks?: TrackMetadataItem[];
  /** Seconds the issued access token stays valid; negative forces a refresh. */
  accessTokenSeconds?: number;
}

export interface FakeBackend {
  fetchImpl: typeof fetch;
  users: StoredUser[];
  tracks: TrackDto[];
  aiConnections: Map<string, StoredAiConnection>;
  lyrics: Map<string, TrackLyricsDto>;
  requests: string[];
  batchCalls: TrackMetadataItem[][];
  reset: () => void;
  setLyrics: (trackId: string, lyrics: Omit<TrackLyricsDto, 'trackId' | 'isEnriched'>) => void;
}

export const DEMO_EMAIL = 'listener@example.com';
export const DEMO_PASSWORD = 'StrongPassword123!';

const normalize = (value: string): string => value.trim().toLowerCase().replace(/\s+/g, ' ');

/** Same idea as the backend fingerprint: normalized title + artist + duration. */
const fingerprint = (item: TrackMetadataItem): string =>
  `${normalize(item.title)}|${normalize(item.artist)}|${item.durationSeconds}`;

const json = (status: number, body: unknown): Response =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });

const problem = (status: number, title: string, detail?: string): Response =>
  new Response(JSON.stringify({ type: 'about:blank', title, detail, status }), {
    status,
    headers: { 'content-type': 'application/problem+json' },
  });

const noContent = (): Response => new Response(null, { status: 204 });

export const createFakeBackend = (options: FakeBackendOptions = {}): FakeBackend => {
  const accessTokenSeconds = options.accessTokenSeconds ?? 900;

  const users: StoredUser[] = [
    {
      userId: '11111111-1111-1111-1111-111111111111',
      email: DEMO_EMAIL,
      password: DEMO_PASSWORD,
      createdAt: '2026-01-01T00:00:00Z',
    },
  ];

  const tokens = new Map<string, TokenPair>();
  const refreshIndex = new Map<string, string>();
  const links = new Map<string, Set<string>>();
  const aiConnections = new Map<string, StoredAiConnection>();
  const tracks: TrackDto[] = [];
  let sequence = 0;

  const backend: FakeBackend = {
    users,
    tracks,
    aiConnections,
    lyrics: new Map<string, TrackLyricsDto>(),
    requests: [],
    batchCalls: [],
    fetchImpl: undefined as unknown as typeof fetch,
    reset,
    setLyrics,
  };

  function reset(): void {
    tokens.clear();
    refreshIndex.clear();
    links.clear();
    aiConnections.clear();
    backend.lyrics.clear();
    tracks.length = 0;
    backend.requests.length = 0;
    backend.batchCalls.length = 0;

    (options.tracks ?? []).forEach((item) => upsert(item, users[0].userId));
  }

  function setLyrics(trackId: string, lyrics: Omit<TrackLyricsDto, 'trackId' | 'isEnriched'>): void {
    const track = tracks.find((item) => item.id === trackId);

    if (!track) {
      return;
    }

    track.hasLyrics = Boolean(lyrics.plainLyrics || lyrics.syncedLyrics);
    track.hasSyncedLyrics = Boolean(lyrics.syncedLyrics);
    backend.lyrics.set(trackId, {
      trackId,
      isEnriched: track.isEnriched,
      plainLyrics: lyrics.plainLyrics,
      syncedLyrics: lyrics.syncedLyrics,
    });
  }

  function issueTokens(userId: string): TokenPair {
    sequence += 1;

    const pair: TokenPair = {
      accessToken: `access-${userId}-${sequence}`,
      refreshToken: `refresh-${userId}-${sequence}`,
      userId,
      expiresAt: Date.now() + accessTokenSeconds * 1000,
    };

    // Negative lifetimes must look expired immediately so the client refreshes.
    if (accessTokenSeconds <= 0) {
      pair.expiresAt = Date.now() - 60_000;
    }

    tokens.set(pair.accessToken, pair);
    refreshIndex.set(pair.refreshToken, pair.accessToken);

    return pair;
  }

  function authHeader(request: Request | string | URL): string {
    if (typeof request === 'string' || request instanceof URL) {
      return '';
    }

    return request.headers.get('authorization') ?? '';
  }

  function userFromRequest(request: Request | string | URL): StoredUser | null {
    const header = authHeader(request);
    const accessToken = header.replace(/^Bearer\s+/i, '');
    const pair = tokens.get(accessToken);

    if (!pair || pair.expiresAt < Date.now()) {
      return null;
    }

    return users.find((user) => user.userId === pair.userId) ?? null;
  }

  function upsert(item: TrackMetadataItem, userId: string): boolean {
    const print = fingerprint(item);
    const existing = tracks.find((track) => fingerprintOf(track) === print);
    const linked = links.get(userId) ?? new Set<string>();

    if (existing) {
      if (linked.has(existing.id)) {
        return false;
      }

      linked.add(existing.id);
      links.set(userId, linked);

      return true;
    }

    const created: TrackDto = {
      id: `track-${tracks.length + 1}`,
      localTrackId: item.localTrackId,
      title: item.title,
      artist: item.artist,
      album: item.album ?? null,
      durationSeconds: item.durationSeconds,
      isEnriched: false,
      hasLyrics: false,
      hasSyncedLyrics: false,
      importedAt: new Date().toISOString(),
    };

    tracks.push(created);
    linked.add(created.id);
    links.set(userId, linked);

    return true;
  }

  function libraryOf(userId: string): TrackDto[] {
    const owned = links.get(userId) ?? new Set<string>();

    return tracks.filter((track) => owned.has(track.id)).reverse();
  }

  async function handle(request: Request): Promise<Response> {
    const url = new URL(request.url, 'http://localhost');
    const path = url.pathname;
    const method = request.method.toUpperCase();

    backend.requests.push(`${method} ${path}${url.search}`);

    const body =
      method === 'GET' || method === 'DELETE'
        ? undefined
        : (((await request.json().catch(() => undefined)) as unknown) ?? undefined);

    if (path === '/api/auth/register' && method === 'POST') {
      const { email, password } = body as { email: string; password: string };

      if (users.some((user) => user.email === email)) {
        return problem(400, 'Validation Failed', 'That email address is already registered.');
      }

      const user: StoredUser = {
        userId: `22222222-0000-0000-0000-${String(users.length).padStart(4, '0')}`,
        email,
        password,
        createdAt: new Date().toISOString(),
      };

      users.push(user);

      const pair = issueTokens(user.userId);

      return json(201, {
        accessToken: pair.accessToken,
        refreshToken: pair.refreshToken,
        expiresInSeconds: accessTokenSeconds,
        userId: user.userId,
      });
    }

    if (path === '/api/auth/login' && method === 'POST') {
      const { email, password } = body as { email: string; password: string };
      const user = users.find((item) => item.email === email && item.password === password);

      if (!user) {
        return problem(401, 'Unauthorized', 'Invalid email or password.');
      }

      const pair = issueTokens(user.userId);

      return json(200, {
        accessToken: pair.accessToken,
        refreshToken: pair.refreshToken,
        expiresInSeconds: accessTokenSeconds,
        userId: user.userId,
      });
    }

    if (path === '/api/auth/refresh' && method === 'POST') {
      const { refreshToken } = body as { refreshToken: string };
      const previous = refreshIndex.get(refreshToken);
      const pair = previous ? tokens.get(previous) : undefined;

      if (!pair) {
        return problem(401, 'Unauthorized', 'Refresh token is not valid.');
      }

      // Rotation: the old pair stops working, mirroring the Identity API behaviour.
      tokens.delete(previous as string);
      refreshIndex.delete(refreshToken);

      const rotated = issueTokens(pair.userId);

      return json(200, {
        accessToken: rotated.accessToken,
        refreshToken: rotated.refreshToken,
        expiresInSeconds: accessTokenSeconds,
        userId: pair.userId,
      });
    }

    if (path === '/api/auth/logout' && method === 'POST') {
      const { refreshToken } = (body ?? {}) as { refreshToken?: string };
      const accessToken = refreshToken ? refreshIndex.get(refreshToken) : undefined;

      if (accessToken) {
        tokens.delete(accessToken);
        refreshIndex.delete(refreshToken as string);
      }

      return noContent();
    }

    if (path === '/api/auth/me' && method === 'GET') {
      const user = userFromRequest(request);

      if (!user) {
        return problem(401, 'Unauthorized', 'Access token is missing or expired.');
      }

      return json(200, {
        userId: user.userId,
        email: user.email,
        createdAt: user.createdAt,
      });
    }

    const user = userFromRequest(request);

    if (!user) {
      return problem(401, 'Unauthorized', 'Access token is missing or expired.');
    }

    if (path === '/api/auth/ai-connection' && method === 'GET') {
      const stored = aiConnections.get(user.userId);

      return stored
        ? json(200, aiMetadata(stored))
        : problem(404, 'Not Found', 'No AI provider connection is configured for this user.');
    }

    if (path === '/api/auth/ai-connection' && method === 'PUT') {
      const input = (body ?? {}) as {
        provider?: AiProvider;
        model?: string;
        apiKey?: string;
        openCodePlan?: OpenCodePlan | null;
      };
      const model = input.model?.trim() ?? '';
      const apiKey = input.apiKey ?? '';
      const plan = input.openCodePlan ?? null;

      if (!input.provider || !AI_PROVIDERS.includes(input.provider)) {
        return problem(400, 'Validation Failed', 'The selected AI provider is not supported.');
      }

      if (!model || model.length > 200) {
        return problem(400, 'Validation Failed', 'The AI model must be nonblank and no longer than 200 characters.');
      }

      if (!apiKey.trim() || apiKey.length > 512) {
        return problem(400, 'Validation Failed', 'The API key must be nonblank and no longer than 512 characters.');
      }

      if (input.provider === 'OpenCode') {
        if (!plan || !OPEN_CODE_PLANS.includes(plan)) {
          return problem(400, 'Validation Failed', 'An OpenCode plan is required for the OpenCode provider.');
        }
      } else if (plan !== null) {
        return problem(400, 'Validation Failed', 'An OpenCode plan is only valid for the OpenCode provider.');
      }

      const previous = aiConnections.get(user.userId);
      const now = new Date().toISOString();
      const stored: StoredAiConnection = {
        provider: input.provider,
        model,
        apiKey,
        openCodePlan: input.provider === 'OpenCode' ? plan : null,
        createdAt: previous?.createdAt ?? now,
        updatedAt: now,
      };

      aiConnections.set(user.userId, stored);

      return json(200, aiMetadata(stored));
    }

    if (path === '/api/auth/ai-connection' && method === 'DELETE') {
      aiConnections.delete(user.userId);

      return noContent();
    }

    if (path === '/api/catalog/tracks/batch' && method === 'POST') {
      const items = ((body as { tracks?: TrackMetadataItem[] })?.tracks ?? []).slice();

      backend.batchCalls.push(items);

      if (items.length === 0) {
        return problem(400, 'Validation Failed', 'The batch must contain at least one track.');
      }

      if (items.length > 500) {
        return problem(400, 'Validation Failed', 'The batch exceeds the maximum allowed number of tracks.');
      }

      const invalid = items.some(
        (item) => !item.localTrackId?.trim() || !item.title?.trim() || !item.artist?.trim(),
      );

      if (invalid) {
        return problem(400, 'Validation Failed', 'Every track must include a local track id, title and artist.');
      }

      let imported = 0;

      items.forEach((item) => {
        if (upsert(item, user.userId)) {
          imported += 1;
        }
      });

      return json(202, { submitted: items.length, imported, duplicates: items.length - imported });
    }

    const lyricsMatch = /^\/api\/catalog\/tracks\/([^/]+)\/lyrics$/.exec(path);

    if (lyricsMatch && method === 'GET') {
      const trackId = decodeURIComponent(lyricsMatch[1] ?? '');
      const owned = libraryOf(user.userId).some((track) => track.id === trackId);

      if (!owned) {
        return problem(404, 'Not Found', 'Track not found in this library.');
      }

      const stored = backend.lyrics.get(trackId);
      const track = tracks.find((item) => item.id === trackId);

      return json(200, {
        trackId,
        isEnriched: track?.isEnriched ?? false,
        plainLyrics: stored?.plainLyrics ?? null,
        syncedLyrics: stored?.syncedLyrics ?? null,
      } satisfies TrackLyricsDto);
    }

    if (path === '/api/catalog/tracks' && method === 'GET') {
      const page = Math.max(1, Number(url.searchParams.get('page') ?? '1'));
      const requested = Number(url.searchParams.get('pageSize') ?? '50');
      const pageSize = requested <= 0 ? 50 : Math.min(requested, 200);
      const owned = libraryOf(user.userId);
      const start = (page - 1) * pageSize;
      const payload: PagedResponse<TrackDto> = {
        items: owned.slice(start, start + pageSize),
        page,
        pageSize,
        totalCount: owned.length,
      };

      return json(200, payload);
    }

    if (path === '/api/catalog/tracks/search' && method === 'GET') {
      const query = (url.searchParams.get('q') ?? '').trim();

      if (query.length === 0) {
        return problem(400, 'Validation Failed', 'A non-empty search query is required.');
      }

      const needle = normalize(query);

      const matches = libraryOf(user.userId).filter((track) => {
        const title = normalize(track.title);
        const artist = normalize(track.artist);

        return (
          title.includes(needle) ||
          artist.includes(needle) ||
          fuzzilyClose(title, needle) ||
          fuzzilyClose(artist, needle)
        );
      });

      return json(200, matches);
    }

    return problem(404, 'Not Found', `No route for ${method} ${path}.`);
  }

  backend.fetchImpl = (async (input: RequestInfo | URL, init?: RequestInit) => {
    // The client calls relative paths, so anchor them before building a Request.
    const absolute = new URL(
      typeof input === 'string' || input instanceof URL ? String(input) : input.url,
      'http://localhost',
    ).href;

    // jsdom's AbortSignal is not an undici AbortSignal, so it cannot go into Request.
    const { signal, ...rest } = init ?? {};

    if (signal?.aborted) {
      throw new DOMException('The operation was aborted.', 'AbortError');
    }

    const request = new Request(
      absolute,
      typeof input === 'object' && !(input instanceof URL)
        ? { method: input.method, headers: input.headers, body: input.body, ...rest }
        : rest,
    );

    return handle(request);
  }) as typeof fetch;

  reset();

  return backend;
};

function aiMetadata(stored: StoredAiConnection): AiConnectionDto {
  return {
    provider: stored.provider,
    model: stored.model,
    openCodePlan: stored.openCodePlan,
    createdAt: stored.createdAt,
    updatedAt: stored.updatedAt,
  };
}

function fingerprintOf(track: TrackDto): string {
  return `${normalize(track.title)}|${normalize(track.artist)}|${track.durationSeconds}`;
}

/** Small similarity stand-in for pg_trgm so typo tests behave sensibly. */
function fuzzilyClose(haystack: string, needle: string): boolean {
  if (needle.length < 4) {
    return false;
  }

  return levenshtein(haystack, needle) <= Math.max(1, Math.floor(needle.length * 0.25));
}

function levenshtein(left: string, right: string): number {
  const rows = left.length + 1;
  const columns = right.length + 1;
  const grid = new Uint16Array(rows * columns);

  for (let row = 0; row < rows; row += 1) {
    grid[row * columns] = row;
  }

  for (let column = 0; column < columns; column += 1) {
    grid[column] = column;
  }

  for (let row = 1; row < rows; row += 1) {
    for (let column = 1; column < columns; column += 1) {
      const cost = left[row - 1] === right[column - 1] ? 0 : 1;

      grid[row * columns + column] = Math.min(
        grid[(row - 1) * columns + column] + 1,
        grid[row * columns + column - 1] + 1,
        grid[(row - 1) * columns + column - 1] + cost,
      );
    }
  }

  return grid[rows * columns - 1];
}

export const seedTracks: TrackMetadataItem[] = [
  {
    localTrackId: 'bohemian-rhapsody-45210034-0',
    title: 'Bohemian Rhapsody',
    artist: 'Queen',
    album: 'A Night at the Opera',
    durationSeconds: 355,
  },
  {
    localTrackId: 'sultans-of-swing-32114022-0',
    title: 'Sultans of Swing',
    artist: 'Dire Straits',
    album: 'Dire Straits',
    durationSeconds: 348,
  },
];
