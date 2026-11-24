import { useState } from 'react';

import { Navigate, useNavigate } from 'react-router-dom';

import Banner from '../components/Feedback/Banner';

import useAuth from '../hooks/useAuth';

const EMAIL_PATTERN = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;

/** Sign in or create an account. */
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

  const submit = async (event: React.FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    setLocalError(null);
    dismissError();

    if (!EMAIL_PATTERN.test(email.trim())) {
      setLocalError('Enter a valid email address.');

      return;
    }

    if (password.length < 8) {
      setLocalError('Use a password with at least 8 characters.');

      return;
    }

    const ok =
      mode === 'login'
        ? await login(email.trim(), password)
        : await register(email.trim(), password);

    if (ok) {
      navigate('/library', { replace: true });
    }
  };

  return (
    <main className='auth-shell'>
      <section className='auth-aside'>
        <div className='auth-brand'>
          <span className='auth-mark'>
            <span className='material-symbols-outlined' aria-hidden='true'>
              graphic_eq
            </span>
          </span>
          <span className='auth-brand-name'>Lyra</span>
        </div>

        <div className='auth-statement'>
          <h1>
            Your music.
            <br />
            <em>Your room.</em>
          </h1>
          <p>
            A private player for the songs already on your device. No uploads, no noise — just the
            collection you built.
          </p>

          <div className='auth-points'>
            <span className='auth-point'>
              <span className='material-symbols-outlined' aria-hidden='true'>
                lock
              </span>
              Audio stays local
            </span>
            <span className='auth-point'>
              <span className='material-symbols-outlined' aria-hidden='true'>
                lyrics
              </span>
              Lyrics that follow the song
            </span>
            <span className='auth-point'>
              <span className='material-symbols-outlined' aria-hidden='true'>
                auto_awesome
              </span>
              Playlists from your library
            </span>
          </div>
        </div>

        <span className='muted small'>Private by design.</span>
      </section>

      <section className='auth-panel'>
        <div className='auth-card'>
          <div>
            <h2>{mode === 'login' ? 'Welcome back' : 'Start your library'}</h2>
            <p className='muted'>
              {mode === 'login'
                ? 'Sign in to pick up where you left off.'
                : 'Create an account for your own local collection.'}
            </p>
          </div>

          <div className='auth-tabs' role='tablist' aria-label='Account'>
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

          <form className='auth-form' noValidate onSubmit={(event) => void submit(event)}>
            <label className='field'>
              <span>Email</span>
              <input
                type='email'
                name='email'
                autoComplete='username'
                value={email}
                placeholder='you@example.com'
                onChange={(event) => setEmail(event.target.value)}
              />
            </label>

            <label className='field'>
              <span>Password</span>
              <input
                type='password'
                name='password'
                autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
                value={password}
                placeholder='At least 8 characters'
                onChange={(event) => setPassword(event.target.value)}
              />
            </label>

            <Banner message={localError ?? error} onDismiss={dismissError} />

            <button type='submit' className='btn btn-primary auth-submit' disabled={busy}>
              <span className='material-symbols-outlined' aria-hidden='true'>
                {busy ? 'hourglass_top' : mode === 'login' ? 'login' : 'person_add'}
              </span>
              {busy ? 'One moment…' : mode === 'login' ? 'Sign in' : 'Create account'}
            </button>
          </form>

          <p className='auth-foot'>
            {mode === 'login'
              ? 'New here? Create an account with the tab above.'
              : 'Your library is private to this account.'}
          </p>
        </div>
      </section>
    </main>
  );
};

export default LoginPage;
