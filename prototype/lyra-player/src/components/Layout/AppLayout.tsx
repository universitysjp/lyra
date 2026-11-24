import { Outlet } from 'react-router-dom';

// components
import Player from '../Player/Player';
import TopBar from '../TopBar/TopBar';

const AppLayout: React.FC = () => (
  <div className='app flex flex-column'>
    <TopBar />

    <main className='content flex flex-1'>
      <Outlet />
    </main>

    <footer className='footnote flex flex-gap-small flex-v-center flex-h-center'>
      <span className='muted small'>
        Prototype for the Lyra backend: Identity login and refresh, encrypted AI provider settings,
        catalog batch import, paged library reads and pg_trgm fuzzy search. Audio stays on this
        device.
      </span>
    </footer>

    <Player />
  </div>
);

export default AppLayout;
