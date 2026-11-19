// components
import Navigation from './navigation/Navigation';

// providers
import ApiProvider from './providers/ApiProvider';
import AuthProvider from './providers/AuthProvider';
import LibraryProvider from './providers/LibraryProvider';
import TrackProvider from './providers/TrackProvider';

// styles
import './styles/player.css';
import './styles/lyra.css';

const App: React.FC = () => (
  <ApiProvider>
    <AuthProvider>
      <LibraryProvider>
        <TrackProvider>
          <Navigation />
        </TrackProvider>
      </LibraryProvider>
    </AuthProvider>
  </ApiProvider>
);

export default App;
