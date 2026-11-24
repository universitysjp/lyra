import { NavLink } from 'react-router-dom';

import useAuth from '../../hooks/useAuth';

const LINKS: [string, string, string][] = [
  ['/library', 'Library', 'home'],
  ['/search', 'Search', 'search'],
  ['/albums', 'Albums', 'album'],
  ['/import', 'Add music', 'add_box'],
  ['/settings', 'AI provider', 'tune'],
];

/** Primary navigation and the signed-in account. */
const Sidebar: React.FC = () => {
  const { session, logout } = useAuth();
  const email = session?.email ?? '';
  const initial = email.slice(0, 1) || 'L';

  return (
    <aside className='sidebar no-select'>
      <NavLink to='/library' className='side-brand' aria-label='Lyra home'>
        <span className='side-mark'>
          <span className='material-symbols-outlined' aria-hidden='true'>
            graphic_eq
          </span>
        </span>
        <span className='side-wordmark'>Lyra</span>
      </NavLink>

      <div className='side-group'>
        <span className='side-label'>Browse</span>
        <nav className='side-nav' aria-label='Sections'>
          {LINKS.map(([to, label, icon]) => (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) => (isActive ? 'side-link active' : 'side-link')}
            >
              <span className='material-symbols-outlined' aria-hidden='true'>
                {icon}
              </span>
              <span>{label}</span>
            </NavLink>
          ))}
        </nav>
      </div>

      {email && (
        <div className='sidebar-foot'>
          <div className='side-user'>
            <span className='side-avatar'>{initial}</span>
            <span className='side-email' title={email}>
              {email}
            </span>
          </div>
          <button
            type='button'
            className='side-signout'
            onClick={() => void logout()}
            title='Sign out'
            aria-label='Sign out'
          >
            <span className='material-symbols-outlined' aria-hidden='true'>
              logout
            </span>
          </button>
        </div>
      )}
    </aside>
  );
};

export default Sidebar;
