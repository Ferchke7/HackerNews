using System.Collections.Concurrent;
using HackerNews.Application.Common;
using HackerNews.Application.Configuration;
using HackerNews.Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HackerNews.Infrastructure.Caching;

/// <summary>
/// Process-local in-memory caching with per-key single-flight for cache misses.
/// </summary>
public sealed class MemoryWithSemaphoreCache(
    IMemoryCache cache,
    IOptions<HackerNewsOptions> options,
    TimeProvider timeProvider,
    ILogger<MemoryWithSemaphoreCache> logger) : IHackerNewsCache
{
    private const string BestStoryIdsCacheKey = "hn_best_story_ids";
    private readonly SemaphoreSlim _storyIdsRefreshLock = new(1, 1);
    private readonly ConcurrentDictionary<int, StoryLockEntry> _storyLocks = new();

    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(options.Value.CacheTtlMinutes);

    public async Task<IReadOnlyList<int>> GetOrFetchStoryIdsAsync(
        Func<CancellationToken, Task<IReadOnlyList<int>>> factory,
        CancellationToken ct = default)
    {
        if (cache.TryGetValue(BestStoryIdsCacheKey, out IReadOnlyList<int>? cachedIds) && cachedIds is not null)
        {
            return cachedIds;
        }

        await _storyIdsRefreshLock.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue(BestStoryIdsCacheKey, out cachedIds) && cachedIds is not null)
            {
                return cachedIds;
            }

            logger.LogInformation("Cache miss: Fetching best story IDs from Hacker News API at {Timestamp}.", timeProvider.GetUtcNow());
            var fetchedIds = await factory(ct);

            cache.Set(BestStoryIdsCacheKey, fetchedIds, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheTtl
            });

            return fetchedIds;
        }
        finally
        {
            _storyIdsRefreshLock.Release();
        }
    }

    public async Task<HackerNewsItem?> GetOrFetchStoryAsync(
        int id,
        Func<CancellationToken, Task<HackerNewsItem?>> factory,
        CancellationToken ct = default)
    {
        var cacheKey = $"hn_story_{id}";

        if (cache.TryGetValue(cacheKey, out HackerNewsItem? cachedStory))
        {
            return cachedStory;
        }

        var (storyLock, semaphore) = await AcquireStoryLockAsync(id, ct);
        try
        {
            if (cache.TryGetValue(cacheKey, out cachedStory))
            {
                return cachedStory;
            }

            var story = await factory(ct);
            cache.Set(cacheKey, story, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheTtl
            });

            return story;
        }
        finally
        {
            semaphore.Release();
            ReleaseStoryLock(id, storyLock);
        }
    }

    private async ValueTask<(StoryLockEntry Entry, SemaphoreSlim Semaphore)> AcquireStoryLockAsync(
        int id,
        CancellationToken ct)
    {
        while (true)
        {
            var entry = _storyLocks.GetOrAdd(id, static _ => new StoryLockEntry());
            Interlocked.Increment(ref entry.References);

            if (!_storyLocks.TryGetValue(id, out var current) || !ReferenceEquals(entry, current))
            {
                ReleaseStoryLock(id, entry);
                continue;
            }

            try
            {
                await entry.Semaphore.WaitAsync(ct);
                return (entry, entry.Semaphore);
            }
            catch
            {
                ReleaseStoryLock(id, entry);
                throw;
            }
        }
    }

    private void ReleaseStoryLock(int id, StoryLockEntry entry)
    {
        if (Interlocked.Decrement(ref entry.References) == 0)
        {
            ((ICollection<KeyValuePair<int, StoryLockEntry>>)_storyLocks)
                .Remove(new KeyValuePair<int, StoryLockEntry>(id, entry));
        }
    }

    private sealed class StoryLockEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int References;
    }
}
