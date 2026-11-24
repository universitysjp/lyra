import { describe, expect, it } from 'vitest';

import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { createFakeBackend, DEMO_EMAIL, DEMO_PASSWORD } from '../test/fakeBackend';
import { loginSession, renderApp } from '../test/render';

describe('SettingsPage', () => {
  it('shows an empty state when no provider is connected', async () => {
    const backend = createFakeBackend();
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/settings', backend, session });

    expect(await screen.findByRole('heading', { name: 'AI provider' })).toBeInTheDocument();
    await waitForSettingsForm();
    expect(screen.getByText('No provider configured yet.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Connect provider/ })).toBeInTheDocument();
  });

  it('saves an OpenAI connection and clears the key from the form', async () => {
    const backend = createFakeBackend();
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/settings', backend, session });

    await waitForSettingsForm();

    await userEvent.type(screen.getByLabelText('Model'), 'gpt-test');
    await userEvent.type(screen.getByLabelText('API key'), 'sk-secret');

    const keyInput = screen.getByLabelText('API key') as HTMLInputElement;
    expect(keyInput.type).toBe('password');

    await userEvent.click(screen.getByRole('button', { name: /Show/ }));
    expect(keyInput.type).toBe('text');

    await userEvent.click(screen.getByRole('button', { name: /Connect provider/ }));

    expect(
      await screen.findByText(/Provider saved. New tracks will use this connection./),
    ).toBeInTheDocument();
    expect(screen.getByText('Connected')).toBeInTheDocument();
    expect(screen.getByText('OpenAI · gpt-test')).toBeInTheDocument();
    expect(keyInput.value).toBe('');

    const stored = backend.aiConnections.get(session.userId);
    expect(stored).toMatchObject({ provider: 'OpenAI', model: 'gpt-test', apiKey: 'sk-secret' });
    expect(backend.requests).toContain('PUT /api/auth/ai-connection');
  });

  it('requires an explicit OpenCode plan', async () => {
    const backend = createFakeBackend();
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/settings', backend, session });

    await waitForSettingsForm();

    await userEvent.selectOptions(screen.getByLabelText('Provider'), 'OpenCode');
    await userEvent.selectOptions(await screen.findByLabelText('Plan'), 'Go');
    await userEvent.type(screen.getByLabelText('Model'), 'deepseek-v4.1-flash');
    await userEvent.type(screen.getByLabelText('API key'), 'oc-secret');
    await userEvent.click(screen.getByRole('button', { name: /Connect provider/ }));

    expect(await screen.findByText(/Provider saved/)).toBeInTheDocument();
    expect(screen.getByText('OpenCode · deepseek-v4.1-flash · Go')).toBeInTheDocument();
    expect(backend.aiConnections.get(session.userId)?.openCodePlan).toBe('Go');
  });

  it('requires inline confirmation before disconnecting', async () => {
    const backend = createFakeBackend();
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/settings', backend, session });

    await waitForSettingsForm();
    await userEvent.type(screen.getByLabelText('Model'), 'gpt-test');
    await userEvent.type(screen.getByLabelText('API key'), 'sk-secret');
    await userEvent.click(screen.getByRole('button', { name: /Connect provider/ }));
    await screen.findByText(/Provider saved/);

    await userEvent.click(screen.getByRole('button', { name: 'Disconnect' }));
    expect(
      screen.getByText(/Disconnect this provider\? Your tracks stay/),
    ).toBeInTheDocument();

    const confirmation = screen.getByText(/Disconnect this provider\?/).closest('.confirm-row');
    expect(confirmation).not.toBeNull();

    await userEvent.click(
      within(confirmation as HTMLElement).getByRole('button', { name: 'Disconnect' }),
    );

    expect(
      await screen.findByText(/Provider disconnected. Tracks stay in your library/),
    ).toBeInTheDocument();
    expect(screen.getByText('Not connected')).toBeInTheDocument();
    expect(backend.aiConnections.has(session.userId)).toBe(false);
  });

  it('validates the model and key before calling Identity', async () => {
    const backend = createFakeBackend();
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/settings', backend, session });

    await waitForSettingsForm();

    await userEvent.click(screen.getByRole('button', { name: /Connect provider/ }));
    expect(screen.getByText('Enter the model you want Lyra to use.')).toBeInTheDocument();

    await userEvent.type(screen.getByLabelText('Model'), 'gpt-test');
    await userEvent.click(screen.getByRole('button', { name: /Connect provider/ }));
    expect(screen.getByText('Enter your provider key.')).toBeInTheDocument();
    expect(backend.requests).not.toContain('PUT /api/auth/ai-connection');
  });
});

const waitForSettingsForm = async (): Promise<void> => {
  await screen.findByText('Not connected');
  await waitFor(() => expect(screen.getByLabelText('Model')).toBeEnabled());
};
