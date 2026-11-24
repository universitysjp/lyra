import { beforeEach, describe, expect, it, vi } from 'vitest';

import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import ImportPanel from './ImportPanel';
import { loginSession, renderWithProviders } from '../../test/render';
import { DEMO_EMAIL, DEMO_PASSWORD, createFakeBackend } from '../../test/fakeBackend';

import type { LocalTrack } from '../../utils/localFiles';

vi.mock('../../utils/filePicker', () => ({
  isAudioFile: (file: File) => file.name.endsWith('.mp3'),
  pickAudioFiles: vi.fn(async () => [
    new File(['audio-bytes'], 'Queen - Bohemian Rhapsody.mp3', { type: 'audio/mpeg' }),
    new File(['audio-bytes'], 'Dire Straits - Sultans of Swing.mp3', { type: 'audio/mpeg' }),
  ]),
}));

// The panel is about wiring; ID3 parsing itself is covered in utils/localFiles.test.ts.
vi.mock('../../utils/localFiles', () => ({
  readLocalTrack: vi.fn(async (file: File): Promise<LocalTrack> => {
    const [artist, title] = file.name.replace('.mp3', '').split(' - ');

    return {
      localTrackId: file.name,
      fileName: file.name,
      title,
      artist,
      album: null,
      trackNumber: null,
      durationSeconds: 300,
      source: 'filename',
      objectUrl: `blob:${file.name}`,
      file,
    };
  }),
  toLocalTrackId(name: string): string {
    return name;
  },
  isImportable(): boolean {
    return true;
  },
  async probeDuration(): Promise<number> {
    return 300;
  },
}));

const findImportResult = (pattern: RegExp) =>
  screen.findByText(
    (_, element) =>
      element?.tagName === 'P' &&
      element.classList.contains('result') &&
      pattern.test(element.textContent ?? ''),
  );

describe('ImportPanel', () => {
  let backend: ReturnType<typeof createFakeBackend>;

  beforeEach(() => {
    backend = createFakeBackend();
  });

  const render = async (): Promise<void> => {
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderWithProviders(<ImportPanel />, { session, backend });
  };

  it('keeps the send action disabled until files are queued', async () => {
    await render();

    expect(screen.getByRole('button', { name: /Add to library/ })).toBeDisabled();
    expect(screen.getByText(/Choose files to see them here before adding them/)).toBeInTheDocument();
  });

  it('previews extracted tags and sends them as one batch', async () => {
    await render();

    await userEvent.click(screen.getByRole('button', { name: 'Choose files' }));

    expect(await screen.findByText('Bohemian Rhapsody')).toBeInTheDocument();
    expect(screen.getByText('Sultans of Swing')).toBeInTheDocument();
        expect(screen.getAllByText('05:00')).toHaveLength(2);

    await userEvent.click(screen.getByRole('button', { name: /Add 2 to library/ }));

    await waitFor(() => expect(backend.batchCalls).toHaveLength(1));

    expect(backend.batchCalls[0].map((item) => item.title)).toEqual([
      'Bohemian Rhapsody',
      'Sultans of Swing',
    ]);
    expect(await findImportResult(/Added 2 tracks/)).toBeInTheDocument();
    expect(screen.getByText('0 files ready')).toBeInTheDocument();
  });

  it('reports duplicates when the same queue is sent twice', async () => {
    await render();

    await userEvent.click(screen.getByRole('button', { name: 'Choose files' }));
    await screen.findByText('Bohemian Rhapsody');
    await userEvent.click(screen.getByRole('button', { name: /Add 2 to library/ }));
    await findImportResult(/Added 2 tracks/);

    await userEvent.click(screen.getByRole('button', { name: 'Choose files' }));
    await userEvent.click(screen.getByRole('button', { name: /Add 2 to library/ }));

    expect(await findImportResult(/2 already in your library/)).toBeInTheDocument();
  });

  it('can drop the queue without sending it', async () => {
    await render();

    await userEvent.click(screen.getByRole('button', { name: 'Choose files' }));
    await screen.findByText('Bohemian Rhapsody');

    await userEvent.click(screen.getByRole('button', { name: 'Clear' }));

    expect(screen.getByText('0 files ready')).toBeInTheDocument();
    expect(backend.batchCalls).toHaveLength(0);
  });

  it('lets the user remove a single row before syncing', async () => {
    await render();

    await userEvent.click(screen.getByRole('button', { name: 'Choose files' }));
    await screen.findByText('Bohemian Rhapsody');

    await userEvent.click(screen.getByRole('button', { name: 'Remove Sultans of Swing' }));

    expect(screen.queryByText('Sultans of Swing')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Add 1 to library/ })).toBeEnabled();
  });
});
