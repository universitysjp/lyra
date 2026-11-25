import { Link } from 'react-router-dom';

import ImportPanel from '../components/Import/ImportPanel';

/** Import local audio into the library. */
const ImportPage: React.FC = () => (
  <div className='page import-page no-select'>
    <header className='page-head'>
      <div>
        <h1 className='page-title'>Add music</h1>
        <p className='page-sub'>Bring in the songs already saved on this device.</p>
      </div>
      <Link to='/library' className='btn btn-ghost'>
        <span className='material-symbols-outlined' aria-hidden='true'>
          library_music
        </span>
        Back to library
      </Link>
    </header>

    <div className='import-layout'>
      <div className='import-main'>
        <ImportPanel />
      </div>

      <aside className='import-aside'>
        <section className='aside-card'>
          <h3>Your files stay here</h3>
          <p>
            Lyra reads the tags in your browser. The audio never leaves this device; only the track
            details are saved to your library.
          </p>
        </section>

        <section className='aside-card'>
          <h3>Supported files</h3>
          <ul className='aside-list'>
            {['MP3', 'M4A', 'FLAC', 'OGG', 'WAV'].map((format) => (
              <li key={format}>
                <span className='material-symbols-outlined' aria-hidden='true'>
                  check_circle
                </span>
                {format}
              </li>
            ))}
          </ul>
        </section>

        <section className='aside-card'>
          <h3>Good tags, better matches</h3>
          <p>
            Title, artist and album help Lyra find the right lyrics and artwork. Files without tags
            still import using their file name.
          </p>
        </section>
      </aside>
    </div>
  </div>
);

export default ImportPage;
