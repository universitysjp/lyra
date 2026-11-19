import { use } from 'react';

import TrackContext from '../contexts/TrackContext';
import type { ITrackContext } from '../contexts/TrackContext';

const useTrack = (): ITrackContext => use(TrackContext);

export default useTrack;
