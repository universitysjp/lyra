namespace Lyra.Enrichment.Worker.Tests;

using Lyra.Enrichment.Worker.Configuration;
using Lyra.Enrichment.Worker.Services.Lyrics;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

public class LrclibRequestThrottleTests
{
    [Fact]
    public async Task WaitAsync_SpacesRequestsByConfiguredInterval()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var throttle = CreateThrottle(clock, 500);

        await throttle.WaitAsync(CancellationToken.None);

        var secondRequest = throttle.WaitAsync(CancellationToken.None);
        Assert.False(secondRequest.IsCompleted);

        clock.Advance(TimeSpan.FromMilliseconds(499));
        await Task.Yield();
        Assert.False(secondRequest.IsCompleted);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        await secondRequest.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task WaitAsync_ZeroIntervalDoesNotDelay()
    {
        var throttle = CreateThrottle(new FakeTimeProvider(), 0);

        await throttle.WaitAsync(CancellationToken.None);
        await throttle.WaitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WaitAsync_CancellationStopsWaitingAndReleasesGate()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var throttle = CreateThrottle(clock, 500);
        await throttle.WaitAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => throttle.WaitAsync(cancellation.Token));

        var next = throttle.WaitAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        await next.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Pause_BlocksRequestsUntilRetryAfterElapses()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var throttle = CreateThrottle(clock, 0);

        throttle.Pause(TimeSpan.FromSeconds(2));
        var next = throttle.WaitAsync(CancellationToken.None);

        clock.Advance(TimeSpan.FromMilliseconds(1999));
        await Task.Yield();
        Assert.False(next.IsCompleted);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        await next.WaitAsync(TimeSpan.FromSeconds(1));
    }

    private static LrclibRequestThrottle CreateThrottle(
        TimeProvider clock,
        int intervalMilliseconds) =>
        new(
            Options.Create(new LrclibOptions
            {
                MinRequestIntervalMilliseconds = intervalMilliseconds
            }),
            clock);
}