import { ApiError } from './http';
import type { LyraClient } from './http';
import type { AiConnectionDto, UpsertAiConnectionRequest } from '../types/lyra';

export interface AiConnectionsApi {
  /** Resolves null when this account has not configured a provider yet. */
  get(signal?: AbortSignal): Promise<AiConnectionDto | null>;
  save(input: UpsertAiConnectionRequest, signal?: AbortSignal): Promise<AiConnectionDto>;
  remove(signal?: AbortSignal): Promise<void>;
}

const PATH = '/api/auth/ai-connection';

/** Identity endpoints for the caller's encrypted AI provider connection. */
export const createAiConnectionsApi = (client: LyraClient): AiConnectionsApi => ({
  async get(signal) {
    try {
      return await client.request<AiConnectionDto>(PATH, { signal });
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        return null;
      }

      throw error;
    }
  },

  save(input, signal) {
    return client.request<AiConnectionDto>(PATH, {
      method: 'PUT',
      body: input,
      signal,
    });
  },

  remove(signal) {
    return client.request<void>(PATH, { method: 'DELETE', signal });
  },
});
