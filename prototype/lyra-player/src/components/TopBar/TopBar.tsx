import { useEffect, useState } from 'react';

import { Link, NavLink } from 'react-router-dom';

// components
import Search from '../Search/Search';

// hooks
import useAuth from '../../hooks/useAuth';

// utils
import { formatTime } from '../../utils/toolbox';

const TABS: [string, string][] = [
  ['/library', 'Library'],
  ['/albums', 'Albums'],
  ['/search', 'Search'],
  ['/import', 'Sync metadata'],
];

/** App chrome: brand, section tabs, the fuzzy search box and the signed-in account state. */
const TopBar: React.FC = () => {
  const { session, status, expiresAt, logout } = useAuth();
  const [now, setNow] = useState<number>(Date.now());

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 10_000);

    return () => window.clearInterval(timer);
  }, []);

  const remainingSeconds = Math.max(0, Math.floor((expiresAt - now) / 1000));

  return (
    <header className='topbar flex flex-gap flex-v-center flex-space-between no-select'>
      <div className='flex flex-gap flex-v-center'>
        <Link to='/library' className='brand'>
          Lyra
        </Link>
        <nav className='tabs flex flex-gap-small' aria-label='Sections'>
          {TABS.map(([path, label]) => (
            <NavLink
              key={path}
              to={path}
              className={({ isActive }) => (isActive ? 'tab active' : 'tab')}
            >
              {label}
            </NavLink>
          ))}
        </nav>
      </div>

      <div className='flex flex-gap-small flex-v-center'>
        <Search />

        {status === 'authenticated' && session && (
          <>
            <span
              className='chip'
              title='Access token lifetime. The client refreshes it through POST /api/auth/refresh.'
            >
              token {formatTime(remainingSeconds)}
            </span>
            <span className='user' title={session.userId}>
              {session.email || session.userId}
            </span>
            <button type='button' className='button black' onClick={() => void logout()}>
              Sign out
            </button>
          </>
        )}
      </div>
    </header>
  );
};

export default TopBar;
