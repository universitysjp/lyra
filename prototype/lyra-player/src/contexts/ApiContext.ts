import { createContext } from 'react';

import type { AiConnectionsApi } from '../api/aiConnections';
import type { AuthApi } from '../api/auth';
import type { CatalogApi } from '../api/catalog';
import type { LyraClient } from '../api/http';

export interface IApiContext {
  client: LyraClient;
  auth: AuthApi;
  catalog: CatalogApi;
  aiConnections: AiConnectionsApi;
}

const unavailable = (): never => {
  throw new Error('ApiProvider has not been mounted.');
};

export const ApiContext = createContext<IApiContext>({
  client: {
    session: unavailable,
    saveSession: unavailable,
    clearSession: unavailable,
    refresh: unavailable,
    request: unavailable,
  },
  auth: { login: unavailable, register: unavailable, me: unavailable, logout: unavailable },
  catalog: {
    listTracks: unavailable,
    searchTracks: unavailable,
    importTracks: unavailable,
    getTrackLyrics: unavailable,
  },
  aiConnections: { get: unavailable, save: unavailable, remove: unavailable },
});

export default ApiContext;
