import { useState } from 'react';

import { Navigate, useNavigate } from 'react-router-dom';

// components
import Banner from '../components/Feedback/Banner';

// hooks
import useAuth from '../hooks/useAuth';

const EMAIL_PATTERN = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;

/**
 * Sign-in and registration against Lyra.Identity.Api through the gateway:
 * POST /api/auth/login and POST /api/auth/register both return an access + refresh token pair.
 */
const LoginPage: React.FC = () => {
  const { status, login, register, busy, error, dismissError } = useAuth();
  const navigate = useNavigate();

  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [email, setEmail] = useState<string>('');
  const [password, setPassword] = useState<string>('');
  const [localError, setLocalError] = useState<string | null>(null);

  if (status === 'authenticated') {
    return <Navigate to='/library' replace />;
  }

  /** Validates locally first, then calls the selected auth endpoint. */
  const submit = async (event: React.FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    setLocalError(null);
    dismissError();

    if (!EMAIL_PATTERN.test(email.trim())) {
      setLocalError('Enter a valid email address.');

      return;
    }

    if (password.length < 8) {
      setLocalError('Identity requires a password of at least 8 characters.');

      return;
    }

    const ok = mode === 'login' ? await login(email.trim(), password) : await register(email.trim(), password);

    if (ok) {
      navigate('/library', { replace: true });
    }
  };

  return (
    <main className='auth flex flex-h-center flex-v-center'>
      <div className='auth-card flex flex-column flex-gap'>
        <div className='flex flex-column flex-gap-small'>
          <h1 className='brand-large'>Lyra</h1>
          <p className='muted'>
            Local-first player: your audio stays on this device, only track metadata is synced.
          </p>
        </div>

        <div className='tabs flex flex-gap-small' role='tablist' aria-label='Account'>
          <button
            type='button'
            role='tab'
            aria-selected={mode === 'login'}
            className={mode === 'login' ? 'tab active' : 'tab'}
            onClick={() => {
              setMode('login');
              setLocalError(null);
            }}
          >
            Sign in
          </button>
          <button
            type='button'
            role='tab'
            aria-selected={mode === 'register'}
            className={mode === 'register' ? 'tab active' : 'tab'}
            onClick={() => {
              setMode('register');
              setLocalError(null);
            }}
          >
            Create account
          </button>
        </div>

        <form
          className='flex flex-column flex-gap'
          noValidate
          onSubmit={(event) => void submit(event)}
        >
          <label className='field flex flex-column flex-gap-small'>
            <span>Email</span>
            <input
              type='email'
              name='email'
              autoComplete='username'
              value={email}
              placeholder='listener@example.com'
              onChange={(event) => setEmail(event.target.value)}
            />
          </label>

          <label className='field flex flex-column flex-gap-small'>
            <span>Password</span>
            <input
              type='password'
              name='password'
              autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
              value={password}
              placeholder='at least 8 characters'
              onChange={(event) => setPassword(event.target.value)}
            />
          </label>

          <Banner message={localError ?? error} onDismiss={dismissError} />

          <button type='submit' className='play-button big flex flex-h-center flex-v-center' disabled={busy}>
            <span className='material-symbols-outlined' aria-hidden='true'>
              {busy ? 'hourglass_top' : mode === 'login' ? 'login' : 'person_add'}
            </span>
            <span className='strong'>{busy ? 'Working...' : mode === 'login' ? 'Sign in' : 'Create account'}</span>
          </button>
        </form>

        <p className='muted small'>
          Runs against the gateway at <code>http://localhost:5000</code> through the Vite dev proxy.
          Start <code>Lyra.Identity.Api</code>, <code>Lyra.Catalog.Api</code> and the{' '}
          <code>Lyra.Gateway</code> first.
        </p>
      </div>
    </main>
  );
};

export default LoginPage;
