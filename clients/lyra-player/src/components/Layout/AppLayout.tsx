import { Outlet } from 'react-router-dom';

import Sidebar from './Sidebar';
import Player from '../Player/Player';
import TopBar from '../TopBar/TopBar';

const AppLayout: React.FC = () => (
  <div className='shell'>
    <Sidebar />

    <div className='shell-main'>
      <TopBar />
      <main className='page-scroll'>
        <Outlet />
      </main>
      <Player />
    </div>
  </div>
);

export default AppLayout;
