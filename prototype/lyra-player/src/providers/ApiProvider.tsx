import { useMemo, useRef } from 'react';
import type { ReactNode } from 'react';

import { createAuthApi } from '../api/auth';
import { createCatalogApi } from '../api/catalog';
import { createLyraClient } from '../api/http';
import { createMemorySessionStore, createSessionStore } from '../api/session';
import ApiContext from '../contexts/ApiContext';
import type { IApiContext } from '../contexts/ApiContext';
import type { Session } from '../api/session';

interface IProps {
  children: ReactNode;
  /** Full override, used by tests to inject a fake transport. */
  value?: IApiContext;
  session?: Session | null;
  fetchImpl?: typeof fetch;
  onSessionLost?: () => void;
}

/** Builds the gateway API objects once for the whole app. */
const ApiProvider: React.FC<IProps> = ({ children, value, session, fetchImpl, onSessionLost }) => {
  const lostListener = useRef(onSessionLost);

  lostListener.current = onSessionLost;

  const created = useMemo<IApiContext>(() => {
    const store =
      session || fetchImpl || !globalThis.localStorage
        ? createMemorySessionStore()
        : createSessionStore();

    if (session) {
      store.save(session);
    }

    const client = createLyraClient({
      store,
      fetchImpl,
      onSessionLost: () => lostListener.current?.(),
    });

    return { client, auth: createAuthApi(client), catalog: createCatalogApi(client) };
    // The provider is mounted once per app; prop changes intentionally create a new client.
  }, [session, fetchImpl]);

  return <ApiContext value={value ?? created}>{children}</ApiContext>;
};

export default ApiProvider;
