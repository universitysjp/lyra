import { beforeEach, describe, expect, it } from 'vitest';

import { createAiConnectionsApi } from './aiConnections';
import { createAuthApi } from './auth';
import { ApiError, createLyraClient } from './http';
import { createMemorySessionStore } from './session';
import { createFakeBackend, DEMO_EMAIL, DEMO_PASSWORD } from '../test/fakeBackend';

describe('ai connections api against the fake gateway', () => {
  let backend: ReturnType<typeof createFakeBackend>;
  let api: ReturnType<typeof createAiConnectionsApi>;

  beforeEach(async () => {
    backend = createFakeBackend();

    const client = createLyraClient({
      store: createMemorySessionStore(),
      fetchImpl: backend.fetchImpl,
    });

    await createAuthApi(client).login(DEMO_EMAIL, DEMO_PASSWORD);
    api = createAiConnectionsApi(client);
  });

  it('returns null when the account has no provider', async () => {
    expect(await api.get()).toBeNull();
  });

  it('stores a provider and returns metadata without the key', async () => {
    const saved = await api.save({
      provider: 'OpenAI',
      model: 'gpt-test',
      apiKey: 'sk-secret',
      openCodePlan: null,
    });

    expect(saved).toMatchObject({ provider: 'OpenAI', model: 'gpt-test', openCodePlan: null });
    expect(JSON.stringify(saved)).not.toContain('sk-secret');
    expect(backend.aiConnections.get('11111111-1111-1111-1111-111111111111')?.apiKey).toBe(
      'sk-secret',
    );
    expect(await api.get()).toMatchObject({ provider: 'OpenAI', model: 'gpt-test' });
  });

  it('keeps the OpenCode plan and removes the connection idempotently', async () => {
    const saved = await api.save({
      provider: 'OpenCode',
      model: 'deepseek-v4.1-flash',
      apiKey: 'oc-secret',
      openCodePlan: 'Go',
    });

    expect(saved.openCodePlan).toBe('Go');

    await api.remove();
    await api.remove();

    expect(await api.get()).toBeNull();
    expect(backend.requests).toContain('DELETE /api/auth/ai-connection');
  });

  it('requires a plan for OpenCode and rejects a plan for other providers', async () => {
    await expect(
      api.save({
        provider: 'OpenCode',
        model: 'model',
        apiKey: 'key',
        openCodePlan: null,
      }),
    ).rejects.toMatchObject({ status: 400 });

    expect(
      await api
        .save({
          provider: 'Anthropic',
          model: 'model',
          apiKey: 'key',
          openCodePlan: 'Go',
        })
        .catch((error: unknown) => error),
    ).toBeInstanceOf(ApiError);
  });
});
