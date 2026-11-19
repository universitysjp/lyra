import { describe, expect, it, vi } from 'vitest';

import { ApiError, createLyraClient } from './http';
import { createMemorySessionStore } from './session';

import type { Session } from './session';

interface Call {
  url: string;
  method: string;
  authorization: string;
  body?: string;
}

const session = (overrides: Partial<Session> = {}): Session => ({
  userId: 'u-1',
  email: 'listener@example.com',
  accessToken: 'access-1',
  refreshToken: 'refresh-1',
  expiresAt: Date.now() + 60_000,
  ...overrides,
});

const json = (body: unknown, status = 200): Response =>
  new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });

const problem = (status: number, title: string, detail?: string): Response =>
  new Response(JSON.stringify({ title, detail, status }), {
    status,
    headers: { 'content-type': 'application/problem+json' },
  });

/** Builds a client over a scripted fetch that records every call. */
const setup = (
  route: (call: Call, index: number) => Response,
  initial: Session | null = session(),
): { client: ReturnType<typeof createLyraClient>; calls: Call[]; store: ReturnType<typeof createMemorySessionStore> } => {
  const calls: Call[] = [];
  const store = createMemorySessionStore();

  if (initial) {
    store.save(initial);
  }

  const fetchImpl = vi.fn(async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    const call: Call = {
      url: String(typeof input === 'object' && !(input instanceof URL) ? (input as Request).url : input),
      method: (init?.method ?? 'GET').toUpperCase(),
      authorization: String((init?.headers as Record<string, string>)?.Authorization ?? ''),
      body: typeof init?.body === 'string' ? init.body : undefined,
    };

    calls.push(call);

    return route(call, calls.length - 1);
  }) as unknown as typeof fetch;

  return { client: createLyraClient({ store, fetchImpl }), calls, store };
};

describe('createLyraClient.request', () => {
  it('sends the bearer token and returns parsed json', async () => {
    const { client, calls } = setup(() => json({ items: [], page: 1, pageSize: 50, totalCount: 0 }));

    await expect(client.request('/api/catalog/tracks?page=2&pageSize=50')).resolves.toEqual({
      items: [],
      page: 1,
      pageSize: 50,
      totalCount: 0,
    });

    expect(calls[0]).toMatchObject({
      url: '/api/catalog/tracks?page=2&pageSize=50',
      method: 'GET',
      authorization: 'Bearer access-1',
    });
  });

  it('omits the authorization header for anonymous calls', async () => {
    const { client, calls } = setup(() => json({ ok: true }));

    await client.request('/api/auth/login', { method: 'POST', auth: false, body: { email: 'a', password: 'b' } });

    expect(calls[0].authorization).toBe('');
    expect(calls[0].body).toBe(JSON.stringify({ email: 'a', password: 'b' }));
  });

  it('surfaces problem+json detail as the error message', async () => {
    const { client } = setup(() => problem(400, 'Validation Failed', 'The batch must contain at least one track.'));

    const error = await client.request('/api/catalog/tracks/batch', {
      method: 'POST',
      body: { tracks: [] },
    }).catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(400);
    expect((error as ApiError).message).toBe('The batch must contain at least one track.');
    expect((error as ApiError).title).toBe('Validation Failed');
  });

  it('falls back to a status message when the error body is not json', async () => {
    const { client } = setup(() => new Response('rate limited', { status: 429 }));

    await expect(client.request('/api/catalog/tracks')).rejects.toThrow(/Rate limited/);
  });

  it('treats 204 responses as no content', async () => {
    const { client } = setup(() => new Response(null, { status: 204 }));

    await expect(client.request('/api/auth/logout', { method: 'POST', body: {} })).resolves.toBeUndefined();
  });
});

describe('token refresh', () => {
  const refreshed = (): Response =>
    json({
      accessToken: 'access-2',
      refreshToken: 'refresh-2',
      expiresInSeconds: 900,
      userId: 'u-1',
    });

  it('refreshes once and replays the failed request with the new token', async () => {
    const { client, calls, store } = setup((call, index) => {
      if (call.url.startsWith('/api/auth/refresh')) {
        return refreshed();
      }

      return index === 0 ? problem(401, 'Unauthorized') : json({ ok: true });
    });

    await expect(client.request('/api/catalog/tracks')).resolves.toEqual({ ok: true });

    expect(calls.map((call) => call.url)).toEqual([
      '/api/catalog/tracks',
      '/api/auth/refresh',
      '/api/catalog/tracks',
    ]);
    expect(calls[2].authorization).toBe('Bearer access-2');
    expect(JSON.parse(calls[1].body as string)).toEqual({ refreshToken: 'refresh-1' });
    expect(store.load()).toMatchObject({ accessToken: 'access-2', refreshToken: 'refresh-2' });
  });

  it('clears the session and notifies when the refresh token is rejected', async () => {
    const onSessionLost = vi.fn();
    const store = createMemorySessionStore();

    store.save(session());

    const client = createLyraClient({
      store,
      onSessionLost,
      fetchImpl: (async () => problem(401, 'Unauthorized', 'Refresh token is not valid.')) as unknown as typeof fetch,
    });

    await expect(client.request('/api/catalog/tracks')).rejects.toThrow(/expired|Unauthorized|not valid/);
    expect(store.load()).toBeNull();
    expect(onSessionLost).toHaveBeenCalledTimes(1);
  });

  it('refreshes up front when the stored access token already looks expired', async () => {
    const { client, calls } = setup((call) =>
      call.url.startsWith('/api/auth/refresh') ? refreshed() : json({ ok: true }),
      session({ expiresAt: Date.now() - 1000 }),
    );

    await client.request('/api/catalog/tracks');

    expect(calls[0].url).toBe('/api/auth/refresh');
    expect(calls[1].authorization).toBe('Bearer access-2');
  });

  it('shares one refresh between concurrent requests', async () => {
    const store = createMemorySessionStore();
    const calls: Call[] = [];
    let unauthorized = 0;

    store.save(session());

    const delay = (ms: number): Promise<void> => new Promise((resolve) => setTimeout(resolve, ms));

    const client = createLyraClient({
      store,
      fetchImpl: (async (input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input);
        const method = (init?.method ?? 'GET').toUpperCase();

        calls.push({
          url,
          method,
          authorization: String((init?.headers as Record<string, string>)?.Authorization ?? ''),
        });

        await delay(5);

        if (url.startsWith('/api/auth/refresh')) {
          return refreshed();
        }

        unauthorized += 1;

        return unauthorized <= 2 ? problem(401, 'Unauthorized') : json({ ok: true });
      }) as unknown as typeof fetch,
    });

    const results = await Promise.all([
      client.request('/api/catalog/tracks'),
      client.request('/api/catalog/tracks/search?q=queen'),
    ]);

    expect(results).toEqual([{ ok: true }, { ok: true }]);
    expect(calls.filter((call) => call.url.startsWith('/api/auth/refresh'))).toHaveLength(1);
  });

  it('does not retry when there is no refresh token', async () => {
    const { client, calls } = setup(() => problem(401, 'Unauthorized'), null);

    await expect(client.request('/api/catalog/tracks')).rejects.toBeInstanceOf(ApiError);
    expect(calls).toHaveLength(1);
  });
});
