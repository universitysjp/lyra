import { beforeEach, describe, expect, it } from 'vitest';

import { createAuthApi } from './auth';
import { ApiError, createLyraClient } from './http';
import { createMemorySessionStore } from './session';
import { DEMO_EMAIL, DEMO_PASSWORD, createFakeBackend } from '../test/fakeBackend';

describe('auth api against the fake gateway', () => {
  let backend: ReturnType<typeof createFakeBackend>;
  let store: ReturnType<typeof createMemorySessionStore>;
  let client: ReturnType<typeof createLyraClient>;

  beforeEach(() => {
    backend = createFakeBackend();
    store = createMemorySessionStore();
    client = createLyraClient({ store, fetchImpl: backend.fetchImpl });
  });

  it('login stores the issued tokens with an absolute expiry', async () => {
    const auth = createAuthApi(client);

    const issued = await auth.login(DEMO_EMAIL, DEMO_PASSWORD);

    expect(issued).toMatchObject({
      email: DEMO_EMAIL,
      userId: '11111111-1111-1111-1111-111111111111',
      accessToken: expect.stringMatching(/^access-/),
    });
    expect(issued.expiresAt).toBeGreaterThan(Date.now());
    expect(store.load()).toEqual(issued);
  });

  it('reports the backend problem detail for bad credentials', async () => {
    const auth = createAuthApi(client);

    const error = await auth.login(DEMO_EMAIL, 'wrong-password').catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(401);
    expect((error as ApiError).message).toBe('Invalid email or password.');
    expect(store.load()).toBeNull();
  });

  it('registers a new account and returns a usable session', async () => {
    const auth = createAuthApi(client);

    const issued = await auth.register('new-listener@example.com', 'AnotherPassword!');

    expect(issued.userId).toMatch(/^22222222-/);
    expect(await auth.me()).toMatchObject({ email: 'new-listener@example.com' });
  });

  it('rejects a duplicate registration', async () => {
    const auth = createAuthApi(client);

    await auth.register('solo@example.com', 'AnotherPassword!');

    await expect(auth.register('solo@example.com', 'AnotherPassword!')).rejects.toThrow(/already registered/);
  });

  it('reads the profile through GET /api/auth/me', async () => {
    const auth = createAuthApi(client);

    await auth.login(DEMO_EMAIL, DEMO_PASSWORD);

    const profile = await auth.me();

    expect(profile).toMatchObject({ email: DEMO_EMAIL, userId: '11111111-1111-1111-1111-111111111111' });
    expect(backend.requests).toContain('POST /api/auth/login');
    expect(backend.requests).toContain('GET /api/auth/me');
  });

  it('rotates tokens on refresh and revokes the old access token', async () => {
    const auth = createAuthApi(client);

    await auth.login(DEMO_EMAIL, DEMO_PASSWORD);

    const before = client.session();
    const rotated = await client.refresh();

    expect(rotated?.accessToken).not.toBe(before?.accessToken);
    expect(await auth.me()).toMatchObject({ email: DEMO_EMAIL });

    await expect(client.request('/api/catalog/tracks')).resolves.toMatchObject({ items: [] });
  });

  it('logout revokes the refresh token and clears the session', async () => {
    const auth = createAuthApi(client);

    await auth.login(DEMO_EMAIL, DEMO_PASSWORD);

    const refreshToken = client.session()?.refreshToken as string;

    await auth.logout();

    expect(client.session()).toBeNull();
    expect(store.load()).toBeNull();
    await expect(
      client.request('/api/auth/refresh', {
        method: 'POST',
        auth: false,
        skipPreRefresh: true,
        body: { refreshToken },
      }),
    ).rejects.toBeInstanceOf(ApiError);
  });
});
