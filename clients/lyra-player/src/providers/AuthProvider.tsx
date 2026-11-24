import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';

import { describeError } from '../api/errors';
import AuthContext from '../contexts/AuthContext';
import type { AuthStatus, IAuthContext } from '../contexts/AuthContext';
import type { Session } from '../api/session';

// hooks
import useApi from '../hooks/useApi';

interface IProps {
  children: ReactNode;
}

/**
 * Owns the signed-in session: restores it from storage on boot, validates it with
 * GET /api/auth/me, and exposes login / register / logout to the UI.
 */
const AuthProvider: React.FC<IProps> = ({ children }) => {
  const { auth, client } = useApi();

  const [status, setStatus] = useState<AuthStatus>('loading');
  const [session, setSession] = useState<Session | null>(client.session());
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<boolean>(false);

  useEffect(() => {
    let cancelled = false;

    const bootstrap = async (): Promise<void> => {
      const stored = client.session();

      if (!stored) {
        if (!cancelled) {
          setStatus('anonymous');
        }

        return;
      }

      try {
        const profile = await auth.me();

        if (cancelled) {
          return;
        }

        const merged: Session = { ...stored, email: profile.email, userId: profile.userId };
        client.saveSession(merged);
        setSession(merged);
        setStatus('authenticated');
      } catch {
        if (!cancelled) {
          client.clearSession();
          setSession(null);
          setStatus('anonymous');
        }
      }
    };

    void bootstrap();

    return () => {
      cancelled = true;
    };
  }, [auth, client]);

  const run = useCallback(async (action: () => Promise<Session>): Promise<boolean> => {
    setBusy(true);
    setError(null);

    try {
      const issued = await action();

      setSession(issued);
      setStatus('authenticated');

      return true;
    } catch (caught) {
      setError(describeError(caught));
      setStatus('anonymous');

      return false;
    } finally {
      setBusy(false);
    }
  }, []);

  const login = useCallback(
    (email: string, password: string) => run(() => auth.login(email, password)),
    [auth, run],
  );

  const register = useCallback(
    (email: string, password: string) => run(() => auth.register(email, password)),
    [auth, run],
  );

  const logout = useCallback(async (): Promise<void> => {
    try {
      await auth.logout();
    } catch (caught) {
      setError(describeError(caught));
    } finally {
      setSession(null);
      setStatus('anonymous');
    }
  }, [auth]);

  const value = useMemo<IAuthContext>(
    () => ({
      status,
      session,
      error,
      busy,
      expiresAt: session?.expiresAt ?? 0,
      login,
      register,
      logout,
      dismissError: () => setError(null),
    }),
    [status, session, error, busy, login, register, logout],
  );

  return <AuthContext value={value}>{children}</AuthContext>;
};

export default AuthProvider;
