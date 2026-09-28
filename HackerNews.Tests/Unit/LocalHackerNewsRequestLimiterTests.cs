using HackerNews.Application.Configuration;
using HackerNews.Infrastructure.Clients;
using Microsoft.Extensions.Options;

namespace HackerNews.Tests.Unit;

[TestFixture]
public sealed class LocalHackerNewsRequestLimiterTests
{
    [Test]
    public async Task Bounds_concurrency_releases_permits_and_honors_cancellation()
    {
        using var limiter = new LocalHackerNewsRequestLimiter(Options.Create(new HackerNewsOptions
        {
            MaxDegreeOfParallelism = 1,
            MaxRequestsPerSecond = 30,
            RequestBurstCapacity = 30,
            UpstreamPermitWaitSeconds = 2
        }));
        await using var heldPermit = await limiter.AcquireAsync();
        using var cancellation = new CancellationTokenSource();

        var waitingPermit = limiter.AcquireAsync(cancellation.Token).AsTask();
        await Task.Delay(100);
        waitingPermit.IsCompleted.ShouldBeFalse();

        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            _ = await waitingPermit;
        });

        await heldPermit.DisposeAsync();
        await using var nextPermit = await limiter.AcquireAsync();
    }

    [Test]
    public async Task Enforces_the_configured_rate_between_upstream_attempts()
    {
        using var limiter = new LocalHackerNewsRequestLimiter(Options.Create(new HackerNewsOptions
        {
            MaxDegreeOfParallelism = 1,
            MaxRequestsPerSecond = 1,
            RequestBurstCapacity = 1,
            UpstreamPermitWaitSeconds = 5
        }));
        var firstPermit = await limiter.AcquireAsync();
        await firstPermit.DisposeAsync();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await using var nextPermit = await limiter.AcquireAsync();

        stopwatch.Elapsed.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(500));
    }
}
