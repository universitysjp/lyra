namespace Lyra.Enrichment.Worker.Services.Lyrics;

using Lyra.Enrichment.Worker.Configuration;
using Microsoft.Extensions.Options;

public sealed class LrclibRequestThrottle : ILrclibRequestThrottle, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _pauseLock = new();
    private readonly TimeSpan _minimumInterval;
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset _lastRequestStartedAt;
    private DateTimeOffset _pausedUntil;

    public LrclibRequestThrottle(
        IOptions<LrclibOptions> options,
        TimeProvider timeProvider)
    {
        var configuredInterval = options.Value.MinRequestIntervalMilliseconds;
        _minimumInterval = configuredInterval <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromMilliseconds(configuredInterval);
        _timeProvider = timeProvider;
        _lastRequestStartedAt = timeProvider.GetUtcNow() - _minimumInterval;
        _pausedUntil = DateTimeOffset.MinValue;
    }

    public async Task WaitAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var now = _timeProvider.GetUtcNow();
            var nextAllowedAt = _lastRequestStartedAt + _minimumInterval;
            DateTimeOffset pausedUntil;

            lock (_pauseLock)
            {
                pausedUntil = _pausedUntil;
            }

            if (pausedUntil > nextAllowedAt)
            {
                nextAllowedAt = pausedUntil;
            }

            if (now < nextAllowedAt)
            {
                await Task.Delay(nextAllowedAt - now, _timeProvider, ct);
            }

            _lastRequestStartedAt = _timeProvider.GetUtcNow();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Pause(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        var resumeAt = _timeProvider.GetUtcNow() + duration;

        lock (_pauseLock)
        {
            if (resumeAt > _pausedUntil)
            {
                _pausedUntil = resumeAt;
            }
        }
    }

    public void Dispose() => _gate.Dispose();
}
