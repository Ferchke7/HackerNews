using System.Threading.RateLimiting;
using HackerNews.Application.Configuration;
using HackerNews.Application.Errors;
using Microsoft.Extensions.Options;

namespace HackerNews.Infrastructure.Clients;

public sealed class LocalHackerNewsRequestLimiter : IHackerNewsRequestLimiter, IDisposable
{
    private const int QueueLimit = 1_000;
    private readonly SemaphoreSlim _concurrency;
    private readonly TokenBucketRateLimiter _rateLimiter;
    private readonly TimeSpan _waitTimeout;

    public LocalHackerNewsRequestLimiter(IOptions<HackerNewsOptions> options)
    {
        var value = options.Value;
        _concurrency = new SemaphoreSlim(value.MaxDegreeOfParallelism, value.MaxDegreeOfParallelism);
        _waitTimeout = TimeSpan.FromSeconds(value.UpstreamPermitWaitSeconds);
        _rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = value.RequestBurstCapacity,
            TokensPerPeriod = value.MaxRequestsPerSecond,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = QueueLimit
        });
    }

    public async ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_waitTimeout);

        try
        {
            using var rateLease = await _rateLimiter.AcquireAsync(1, timeout.Token);
            if (!rateLease.IsAcquired)
            {
                throw new UpstreamUnavailableException("The local Hacker News request budget is exhausted.");
            }

            await _concurrency.WaitAsync(timeout.Token);
            return new SemaphoreLease(_concurrency);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpstreamUnavailableException("Timed out waiting for a local Hacker News request permit.", exception);
        }
    }

    public void Dispose()
    {
        _rateLimiter.Dispose();
        _concurrency.Dispose();
    }

    private sealed class SemaphoreLease(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        private int _disposed;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                semaphore.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
