namespace Lyra.Enrichment.Worker.Tests.TestDoubles;

using Lyra.Enrichment.Worker.Models;
using Lyra.Enrichment.Worker.Services.Caching;

internal sealed class FakeEnrichmentResultStore : IEnrichmentResultStore
{
    private readonly Queue<TrackEnrichmentResult?> _getResponses = new();

    public TrackEnrichmentResult? CachedResult { get; set; }

    public bool LockAvailable { get; set; } = true;

    public int GetCalls { get; private set; }

    public int SetCalls { get; private set; }

    public int LockCalls { get; private set; }

    public int LockDisposals { get; private set; }

    public TrackEnrichmentResult? LastSetResult { get; private set; }

    public void EnqueueGet(params TrackEnrichmentResult?[] results)
    {
        foreach (var result in results)
        {
            _getResponses.Enqueue(result);
        }
    }

    public Task<TrackEnrichmentResult?> GetAsync(Guid trackId, CancellationToken ct)
    {
        GetCalls++;
        if (_getResponses.Count > 0)
        {
            return Task.FromResult(_getResponses.Dequeue());
        }

        return Task.FromResult(CachedResult);
    }

    public Task SetAsync(
        Guid trackId,
        TrackEnrichmentResult result,
        CancellationToken ct)
    {
        SetCalls++;
        LastSetResult = result;
        CachedResult = result;
        return Task.CompletedTask;
    }

    public Task<ITrackEnrichmentLock?> TryAcquireLockAsync(
        Guid trackId,
        CancellationToken ct)
    {
        LockCalls++;
        return Task.FromResult<ITrackEnrichmentLock?>(
            LockAvailable
                ? new FakeTrackEnrichmentLock(() => LockDisposals++)
                : null);
    }

    private sealed class FakeTrackEnrichmentLock(Action onDispose) : ITrackEnrichmentLock
    {
        private int _disposed;

        public string OwnerToken { get; } = Guid.NewGuid().ToString("N");

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                onDispose();
            }

            return ValueTask.CompletedTask;
        }
    }
}
