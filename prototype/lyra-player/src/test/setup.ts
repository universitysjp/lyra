import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, beforeEach } from 'vitest';

import { clearLocalAudio } from '../utils/localAudioStore';

afterEach(() => {
  cleanup();
  clearLocalAudio();
});

beforeEach(() => {
  // jsdom does not implement object URLs; the app only passes them through to <audio>.
  let counter = 0;

  URL.createObjectURL = () => `blob:local-${(counter += 1)}`;
  URL.revokeObjectURL = () => undefined;
});
