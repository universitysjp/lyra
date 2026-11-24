import { useEffect, useMemo, useState } from 'react';

import { Link } from 'react-router-dom';

import Banner from '../components/Feedback/Banner';

import useApi from '../hooks/useApi';

import { describeError } from '../api/errors';

import type { AiConnectionDto, AiProvider, OpenCodePlan } from '../types/lyra';

interface IProviderDetails {
  label: string;
  modelPlaceholder: string;
  hint: string;
}

const PROVIDERS: Record<AiProvider, IProviderDetails> = {
  OpenAI: {
    label: 'OpenAI',
    modelPlaceholder: 'gpt-5-mini',
    hint: 'Use an OpenAI Platform API key. ChatGPT subscriptions are not API keys.',
  },
  Gemini: {
    label: 'Google Gemini',
    modelPlaceholder: 'gemini-3.5-flash',
    hint: 'Use a Gemini API key from Google AI Studio.',
  },
  OpenCode: {
    label: 'OpenCode',
    modelPlaceholder: 'deepseek-v4.1-flash',
    hint: 'Choose a paid model or a Go plan. Free-tier models are not available to external apps.',
  },
  Anthropic: {
    label: 'Anthropic',
    modelPlaceholder: 'claude-sonnet-4-6',
    hint: 'Use an Anthropic API key and a model that supports structured JSON output.',
  },
};

const PROVIDER_ORDER: AiProvider[] = ['OpenAI', 'Gemini', 'OpenCode', 'Anthropic'];

const providerSummary = (connection: AiConnectionDto): string => {
  const base = `${PROVIDERS[connection.provider].label} · ${connection.model}`;

  if (connection.provider !== 'OpenCode' || !connection.openCodePlan) {
    return base;
  }

  return `${base} · ${connection.openCodePlan === 'Go' ? 'Go' : 'Pay as you go'}`;
};

const formatUpdatedAt = (value: string): string => {
  const date = new Date(value);

  return Number.isNaN(date.valueOf()) ? value : date.toLocaleString();
};

/** Connect the listener's own AI provider for mood, theme and energy enrichment. */
const SettingsPage: React.FC = () => {
  const { aiConnections } = useApi();

  const [connection, setConnection] = useState<AiConnectionDto | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [saving, setSaving] = useState<boolean>(false);
  const [removing, setRemoving] = useState<boolean>(false);
  const [confirmingDelete, setConfirmingDelete] = useState<boolean>(false);
  const [showKey, setShowKey] = useState<boolean>(false);
  const [provider, setProvider] = useState<AiProvider>('OpenAI');
  const [model, setModel] = useState<string>('');
  const [apiKey, setApiKey] = useState<string>('');
  const [openCodePlan, setOpenCodePlan] = useState<OpenCodePlan>('PayAsYouGo');
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const selectedProvider = useMemo(() => PROVIDERS[provider], [provider]);
  const busy = loading || saving || removing;

  useEffect(() => {
    const controller = new AbortController();

    setLoading(true);

    aiConnections
      .get(controller.signal)
      .then((current) => {
        setConnection(current);

        if (current) {
          setProvider(current.provider);
          setModel(current.model);

          if (current.openCodePlan) {
            setOpenCodePlan(current.openCodePlan);
          }
        }
      })
      .catch((caught: unknown) => {
        if (!controller.signal.aborted) {
          setError(describeError(caught));
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setLoading(false);
        }
      });

    return () => controller.abort();
  }, [aiConnections]);

  const chooseProvider = (next: AiProvider): void => {
    setProvider(next);
    setError(null);
    setNotice(null);

    if (next === 'OpenCode') {
      setOpenCodePlan(connection?.openCodePlan ?? 'PayAsYouGo');
    }
  };

  const save = async (event: React.FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    setError(null);
    setNotice(null);

    if (!model.trim()) {
      setError('Enter the model you want Lyra to use.');

      return;
    }

    if (!apiKey.trim()) {
      setError(
        connection
          ? 'Enter the key again to save changes. It cannot be read back.'
          : 'Enter your provider key.',
      );

      return;
    }

    setSaving(true);

    try {
      const saved = await aiConnections.save({
        provider,
        model: model.trim(),
        apiKey,
        openCodePlan: provider === 'OpenCode' ? openCodePlan : null,
      });

      setConnection(saved);
      setApiKey('');
      setShowKey(false);
      setNotice('Provider saved. New tracks will use this connection.');
    } catch (caught: unknown) {
      setError(describeError(caught));
    } finally {
      setSaving(false);
    }
  };

  const disconnect = async (): Promise<void> => {
    setRemoving(true);
    setError(null);
    setNotice(null);

    try {
      await aiConnections.remove();
      setConnection(null);
      setConfirmingDelete(false);
      setProvider('OpenAI');
      setModel('');
      setApiKey('');
      setOpenCodePlan('PayAsYouGo');
      setNotice('Provider disconnected. Tracks stay in your library.');
    } catch (caught: unknown) {
      setError(describeError(caught));
    } finally {
      setRemoving(false);
    }
  };

  return (
    <div className='page settings-page no-select'>
      <header className='page-head'>
        <div>
          <h1 className='page-title'>AI provider</h1>
          <p className='page-sub'>
            Choose the model that reads your lyrics and describes each track.
          </p>
        </div>
      </header>

      <Banner message={error} onDismiss={() => setError(null)} />
      <Banner kind='success' message={notice} onDismiss={() => setNotice(null)} />

      <div className='settings-grid'>
        <section className='settings-panel' aria-busy={loading}>
          <div className='settings-state'>
            <div className='flex flex-column flex-gap-small'>
              <span className={connection ? 'status-pill connected' : 'status-pill'}>
                {loading ? 'Checking' : connection ? 'Connected' : 'Not connected'}
              </span>
              <strong>
                {connection ? providerSummary(connection) : 'No provider configured yet.'}
              </strong>
              {connection && (
                <span className='muted small'>Updated {formatUpdatedAt(connection.updatedAt)}</span>
              )}
            </div>

            {connection && !confirmingDelete && (
              <button
                type='button'
                className='btn btn-ghost'
                onClick={() => setConfirmingDelete(true)}
                disabled={busy}
              >
                Disconnect
              </button>
            )}
          </div>

          {confirmingDelete && (
            <div className='confirm-row'>
              <p>
                Disconnect this provider? Your tracks stay, but new enrichment will pause until you
                connect another one.
              </p>
              <div className='flex flex-gap-small'>
                <button
                  type='button'
                  className='btn btn-danger btn-sm'
                  onClick={() => void disconnect()}
                  disabled={removing}
                >
                  {removing ? 'Disconnecting…' : 'Disconnect'}
                </button>
                <button
                  type='button'
                  className='btn btn-ghost btn-sm'
                  onClick={() => setConfirmingDelete(false)}
                  disabled={removing}
                >
                  Keep it
                </button>
              </div>
            </div>
          )}

          <form className='auth-form' onSubmit={(event) => void save(event)}>
            <label className='field'>
              <span>Provider</span>
              <select
                value={provider}
                onChange={(event) => chooseProvider(event.target.value as AiProvider)}
                disabled={busy}
              >
                {PROVIDER_ORDER.map((value) => (
                  <option key={value} value={value}>
                    {PROVIDERS[value].label}
                  </option>
                ))}
              </select>
            </label>

            <label className='field'>
              <span>Model</span>
              <input
                type='text'
                name='model'
                value={model}
                placeholder={selectedProvider.modelPlaceholder}
                autoComplete='off'
                spellCheck={false}
                onChange={(event) => setModel(event.target.value)}
                disabled={busy}
              />
            </label>

            {provider === 'OpenCode' && (
              <label className='field'>
                <span>Plan</span>
                <select
                  value={openCodePlan}
                  onChange={(event) => setOpenCodePlan(event.target.value as OpenCodePlan)}
                  disabled={busy}
                >
                  <option value='PayAsYouGo'>Pay as you go</option>
                  <option value='Go'>Go subscription</option>
                </select>
              </label>
            )}

            <label className='field'>
              <span>API key</span>
              <div className='key-control'>
                <input
                  type={showKey ? 'text' : 'password'}
                  name='apiKey'
                  value={apiKey}
                  placeholder={
                    connection ? 'Enter the key again to save' : 'Paste your provider key'
                  }
                  autoComplete='off'
                  spellCheck={false}
                  onChange={(event) => setApiKey(event.target.value)}
                  disabled={busy}
                />
                <button
                  type='button'
                  className='btn btn-ghost'
                  aria-pressed={showKey}
                  onClick={() => setShowKey((value) => !value)}
                  disabled={busy || apiKey.length === 0}
                >
                  {showKey ? 'Hide' : 'Show'}
                </button>
              </div>
            </label>

            <p className='muted small'>{selectedProvider.hint}</p>

            <div className='settings-actions'>
              <button type='submit' className='btn btn-primary' disabled={busy}>
                <span className='material-symbols-outlined' aria-hidden='true'>
                  key
                </span>
                {saving ? 'Saving…' : connection ? 'Save changes' : 'Connect provider'}
              </button>
              <Link to='/import' className='btn btn-ghost'>
                Add music
              </Link>
            </div>
          </form>
        </section>

        <aside className='settings-aside'>
          <h2>What this powers</h2>
          <p>
            Every new track can carry a mood, a few themes and an energy level. That is what makes
            search and playlists feel aware of the music, not just the file names.
          </p>
          <p>
            Lyrics are stored as soon as they are found. If the provider is unavailable, the words
            stay in your library and enrichment catches up later.
          </p>
          <p>
            Your key is encrypted, belongs to your provider account, and is never shown back to you.
          </p>
        </aside>
      </div>
    </div>
  );
};

export default SettingsPage;
