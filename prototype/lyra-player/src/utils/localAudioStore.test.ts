import { describe, expect, it, vi } from 'vitest';

import {
  attachLocalAudio,
  clearLocalAudio,
  hasLocalAudio,
  localAudioCount,
  localAudioUrl,
  localAudioVersion,
  registerLocalAudio,
  subscribeLocalAudio,
} from './localAudioStore';

const file = (name: string): File => ({ name }) as unknown as File;

describe('localAudioStore', () => {
  it('registers a URL for a local track id', () => {
    registerLocalAudio('track-a', 'blob:one');

    expect(localAudioUrl('track-a')).toBe('blob:one');
    expect(hasLocalAudio('track-a')).toBe(true);
    expect(localAudioUrl('track-b')).toBe('');
    expect(hasLocalAudio('track-b')).toBe(false);
  });

  it('creates the object URL when attaching a file', () => {
    const url = attachLocalAudio('track-c', file('c.mp3'));

    expect(url).toMatch(/^blob:/);
    expect(localAudioUrl('track-c')).toBe(url);
  });

  it('replaces an existing registration and revokes the old URL', () => {
    const revoke = vi.spyOn(URL, 'revokeObjectURL');

    registerLocalAudio('track-d', 'blob:old');
    registerLocalAudio('track-d', 'blob:new');

    expect(localAudioUrl('track-d')).toBe('blob:new');
    expect(revoke).toHaveBeenCalledWith('blob:old');
    expect(localAudioCount()).toBe(1);
  });

  it('notifies subscribers and advances the version counter', () => {
    const listener = vi.fn();
    const before = localAudioVersion();

    const unsubscribe = subscribeLocalAudio(listener);
    registerLocalAudio('track-e', 'blob:e');

    expect(listener).toHaveBeenCalledTimes(1);
    expect(localAudioVersion()).toBe(before + 1);

    unsubscribe();
    registerLocalAudio('track-f', 'blob:f');

    expect(listener).toHaveBeenCalledTimes(1);
    expect(localAudioCount()).toBe(2);
  });

  it('clears every registration', () => {
    registerLocalAudio('track-g', 'blob:g');
    clearLocalAudio();

    expect(localAudioCount()).toBe(0);
    expect(hasLocalAudio('track-g')).toBe(false);
  });
});
