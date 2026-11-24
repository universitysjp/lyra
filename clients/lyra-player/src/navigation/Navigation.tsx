import { Navigate, Route, Routes } from 'react-router-dom';

import type { ReactNode } from 'react';

// components
import AppLayout from '../components/Layout/AppLayout';

// hooks
import useAuth from '../hooks/useAuth';

// pages
import AlbumPage from '../pages/AlbumPage';
import AlbumsPage from '../pages/AlbumsPage';
import LoginPage from '../pages/LoginPage';
import ImportPage from '../pages/ImportPage';
import LibraryPage from '../pages/LibraryPage';
import SearchPage from '../pages/SearchPage';
import SettingsPage from '../pages/SettingsPage';

/** Blocks the player UI until the session is validated against the Identity API. */
const RequireAuth: React.FC<{ children: ReactNode }> = ({ children }) => {
  const { status } = useAuth();

  if (status === 'loading') {
    return (
      <div className='splash flex flex-h-center flex-v-center'>
        <p className='muted'>Restoring your Lyra session...</p>
      </div>
    );
  }

  if (status === 'anonymous') {
    return <Navigate to='/login' replace />;
  }

  return <>{children}</>;
};

const Navigation: React.FC = () => (
  <Routes>
    <Route path='/login' element={<LoginPage />} />

    <Route
      element={
        <RequireAuth>
          <AppLayout />
        </RequireAuth>
      }
    >
      <Route path='/' element={<Navigate to='/library' replace />} />
      <Route path='/library' element={<LibraryPage />} />
      <Route path='/albums' element={<AlbumsPage />} />
      <Route path='/album/:id' element={<AlbumPage />} />
      <Route path='/search' element={<SearchPage />} />
      <Route path='/import' element={<ImportPage />} />
      <Route path='/settings' element={<SettingsPage />} />
      <Route path='*' element={<Navigate to='/library' replace />} />
    </Route>
  </Routes>
);

export default Navigation;
