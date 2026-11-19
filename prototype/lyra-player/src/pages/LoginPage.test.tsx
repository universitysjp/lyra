import { describe, expect, it } from 'vitest';

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { loginSession, renderApp } from '../test/render';
import { DEMO_EMAIL, DEMO_PASSWORD, createFakeBackend } from '../test/fakeBackend';

describe('LoginPage', () => {
  it('signs in with valid credentials and opens the library', async () => {
    const backend = createFakeBackend();

    renderApp({ route: '/login', backend, session: null });

    await userEvent.type(screen.getByLabelText('Email'), DEMO_EMAIL);
    await userEvent.type(screen.getByLabelText('Password'), DEMO_PASSWORD);
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('heading', { name: 'Your library' })).toBeInTheDocument();
    expect(backend.requests).toContain('POST /api/auth/login');
  });

  it('refuses credentials the identity service rejects', async () => {
    const backend = createFakeBackend();

    renderApp({ route: '/login', backend, session: null });

    await userEvent.type(screen.getByLabelText('Email'), DEMO_EMAIL);
    await userEvent.type(screen.getByLabelText('Password'), 'not-the-password');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Invalid email or password.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('validates before calling the backend', async () => {
    const backend = createFakeBackend();

    renderApp({ route: '/login', backend, session: null });

    await userEvent.type(screen.getByLabelText('Email'), 'not-an-email');
    await userEvent.type(screen.getByLabelText('Password'), 'short');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Enter a valid email address.')).toBeInTheDocument();
    expect(backend.requests).toHaveLength(0);
  });

  it('registers a new account and signs the user straight in', async () => {
    const backend = createFakeBackend();

    renderApp({ route: '/login', backend, session: null });

    await userEvent.click(screen.getByRole('tab', { name: 'Create account' }));
    await userEvent.type(screen.getByLabelText('Email'), 'fresh@example.com');
    await userEvent.type(screen.getByLabelText('Password'), 'AnotherPassword!');
    await userEvent.click(screen.getByRole('button', { name: 'Create account' }));

    expect(await screen.findByRole('heading', { name: 'Your library' })).toBeInTheDocument();
    expect(backend.requests).toContain('POST /api/auth/register');
  });

  it('keeps a signed-in user out of the login page', async () => {
    const backend = createFakeBackend();
    const session = await loginSession(backend, DEMO_EMAIL, DEMO_PASSWORD);

    renderApp({ route: '/login', backend, session });

    expect(await screen.findByRole('heading', { name: 'Your library' })).toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'Create account' })).not.toBeInTheDocument();
  });
});
