import Search from '../Search/Search';

/** Sticky app header with the global search field. */
const TopBar: React.FC = () => (
  <header className='topbar no-select'>
    <span className='topbar-note'>
      <span className='material-symbols-outlined' aria-hidden='true'>
        lock
      </span>
      Your audio stays on this device
    </span>
    <Search />
  </header>
);

export default TopBar;
