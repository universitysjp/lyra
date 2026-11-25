import { useSyncExternalStore } from 'react';

import { localAudioVersion, subscribeLocalAudio } from '../utils/localAudioStore';

/**
 * Re-renders whenever the user attaches local audio, so rows gain a play button
 * without any backend round-trip.
 */
const useLocalAudioVersion = (): number =>
  useSyncExternalStore(subscribeLocalAudio, localAudioVersion, localAudioVersion);

export default useLocalAudioVersion;
