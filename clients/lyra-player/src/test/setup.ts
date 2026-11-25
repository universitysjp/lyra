import '@testing-library/jest-dom/vitest';
import { cleanup, configure } from '@testing-library/react';
import { afterEach, beforeEach } from 'vite-plus/test';

import { clearLocalAudio } from '../utils/localAudioStore';

// Vitest 5 starts a worker per file; the 400 ms search debounce plus a cold render
// can exceed the 1 s default. Give async queries a little more room.
configure({ asyncUtilTimeout: 2000 });

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
