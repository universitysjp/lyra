namespace Lyra.Enrichment.Worker.Services.Lyrics;

using Lyra.Enrichment.Worker.Configuration;
using Microsoft.Extensions.Options;

public sealed class LrclibRequestThrottle : ILrclibRequestThrottle, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeSpan _minimumInterval;
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset _lastRequestStartedAt;

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
    }

    public async Task WaitAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_minimumInterval == TimeSpan.Zero)
            {
                _lastRequestStartedAt = _timeProvider.GetUtcNow();
                return;
            }

            var now = _timeProvider.GetUtcNow();
            var nextAllowedAt = _lastRequestStartedAt + _minimumInterval;
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

    public void Dispose() => _gate.Dispose();
}
