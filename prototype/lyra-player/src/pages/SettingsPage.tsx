import { useEffect, useMemo, useState } from 'react';

import { Link } from 'react-router-dom';

// components
import Banner from '../components/Feedback/Banner';

// hooks
import useApi from '../hooks/useApi';

// utils
import { describeError } from '../api/errors';

import type { AiConnectionDto, AiProvider, OpenCodePlan } from '../types/lyra';

interface IProviderDetails {
  label: string;
  modelPlaceholder: string;
  hint: string;
}

const PROVIDERS: Record<AiProvider, IProviderDetails> = {
  OpenAI: {
    label: 'OpenAI API',
    modelPlaceholder: 'gpt-5-mini',
    hint: 'Uses an OpenAI Platform API key. ChatGPT Plus or Pro subscriptions are not API keys.',
  },
  Gemini: {
    label: 'Google Gemini',
    modelPlaceholder: 'gemini-3.5-flash',
    hint: 'Uses a Gemini API key from Google AI Studio.',
  },
  OpenCode: {
    label: 'OpenCode Console',
    modelPlaceholder: 'deepseek-v4.1-flash',
    hint: 'OpenCode free-tier models are not available to external clients. Use a paid model or Go plan.',
  },
  Anthropic: {
    label: 'Anthropic Claude',
    modelPlaceholder: 'claude-sonnet-4-6',
    hint: 'Uses an Anthropic API key. Choose a model that supports structured JSON output.',
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

/**
 * BYOK settings for enrichment. The backend stores the provider credential encrypted
 * in identity_db and only ever returns provider/model/plan metadata.
 */
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
      setError('Enter the model ID you want Lyra to call.');

      return;
    }

    if (!apiKey.trim()) {
      setError(
        connection
          ? 'Enter the API key again to save changes. Lyra cannot read the stored key back.'
          : 'Enter your provider API key.',
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
      setNotice('AI provider saved. New enrichment jobs will use this connection.');
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
      setNotice('AI provider disconnected. Tracks will remain unenriched until you reconnect.');
    } catch (caught: unknown) {
      setError(describeError(caught));
    } finally {
      setRemoving(false);
    }
  };

  return (
    <div className='settings flex flex-column flex-gap no-select'>
      <div className='container flex flex-column flex-gap'>
        <section className='hero flex flex-column flex-gap-small'>
          <h1>AI enrichment</h1>
          <em>Your provider, your model, your API key.</em>
          <p className='muted'>
            Lyra sends lyrics and track metadata to the provider you choose. The key is encrypted
            in <code>identity_db</code>, used only by the enrichment worker, and never returned by
            the API.
          </p>
        </section>

        <Banner message={error} onDismiss={() => setError(null)} />
        <Banner kind='success' message={notice} onDismiss={() => setNotice(null)} />

        <div className='settings-layout'>
          <section className='settings-form flex flex-column flex-gap' aria-busy={loading}>
            <div className='connection-state flex flex-gap flex-v-center flex-space-between'>
              <div className='flex flex-column flex-gap-small'>
                <span className={connection ? 'chip chip-ok' : 'chip chip-muted'}>
                  {loading ? 'Checking connection' : connection ? 'Connected' : 'Not connected'}
                </span>
                <strong>
                  {connection
                    ? providerSummary(connection)
                    : 'No provider is configured for this account.'}
                </strong>
                {connection && (
                  <span className='muted small'>
                    Updated {formatUpdatedAt(connection.updatedAt)}
                  </span>
                )}
              </div>

              {connection && !confirmingDelete && (
                <button
                  type='button'
                  className='button black'
                  onClick={() => setConfirmingDelete(true)}
                  disabled={busy}
                >
                  Disconnect
                </button>
              )}
            </div>

            {confirmingDelete && (
              <div className='confirm-row flex flex-gap flex-v-center flex-space-between'>
                <p className='muted small'>
                  Disconnect this provider? Imported tracks stay in your library, but enrichment
                  pauses until another provider is connected.
                </p>
                <div className='flex flex-gap-small'>
                  <button
                    type='button'
                    className='button danger'
                    onClick={() => void disconnect()}
                    disabled={removing}
                  >
                    {removing ? 'Disconnecting...' : 'Disconnect provider'}
                  </button>
                  <button
                    type='button'
                    className='button black'
                    onClick={() => setConfirmingDelete(false)}
                    disabled={removing}
                  >
                    Keep it
                  </button>
                </div>
              </div>
            )}

            <form className='flex flex-column flex-gap' onSubmit={(event) => void save(event)}>
              <label className='field flex flex-column flex-gap-small'>
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

              <label className='field flex flex-column flex-gap-small'>
                <span>Model ID</span>
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
                <label className='field flex flex-column flex-gap-small'>
                  <span>OpenCode plan</span>
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

              <label className='field flex flex-column flex-gap-small'>
                <span>API key</span>
                <div className='key-control flex flex-gap-small'>
                  <input
                    type={showKey ? 'text' : 'password'}
                    name='apiKey'
                    value={apiKey}
                    placeholder={
                      connection
                        ? 'Enter the key again to save changes'
                        : 'Paste your provider API key'
                    }
                    autoComplete='off'
                    spellCheck={false}
                    onChange={(event) => setApiKey(event.target.value)}
                    disabled={busy}
                  />
                  <button
                    type='button'
                    className='button black'
                    aria-pressed={showKey}
                    onClick={() => setShowKey((value) => !value)}
                    disabled={busy || apiKey.length === 0}
                  >
                    {showKey ? 'Hide' : 'Show'}
                  </button>
                </div>
              </label>

              <p className='muted small provider-hint'>{selectedProvider.hint}</p>

              <div className='flex flex-gap flex-v-center settings-actions'>
                <button type='submit' className='button primary' disabled={busy}>
                  <span className='material-symbols-outlined' aria-hidden='true'>
                    key
                  </span>
                  <span>
                    {saving ? 'Saving...' : connection ? 'Save connection' : 'Connect provider'}
                  </span>
                </button>
                <Link to='/import' className='button black'>
                  Sync tracks
                </Link>
              </div>
            </form>
          </section>

          <aside className='settings-guidance flex flex-column flex-gap-small'>
            <h2>What happens after you connect</h2>
            <p>
              New imports publish an enrichment job. The worker asks your provider for mood, themes
              and energy, then stores the result on the shared track record.
            </p>
            <p>
              Usage is billed by your provider account. Entering an API key here does not change
              your provider plan or create a Lyra subscription.
            </p>
            <p>
              Tracks imported before connecting remain in the library and stay unenriched. Future
              imports are processed with the connection you save here.
            </p>
          </aside>
        </div>
      </div>
    </div>
  );
};

export default SettingsPage;
