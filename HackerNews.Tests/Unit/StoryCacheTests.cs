using HackerNews.Application.Configuration;
using HackerNews.Application.Mappers;
using HackerNews.Domain.Models;
using HackerNews.Infrastructure.Caching;
using HackerNews.Tests.Builders;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace HackerNews.Tests.Unit;

[TestFixture]
public sealed class StoryCacheTests
{
    private readonly IOptions<HackerNewsOptions> _options = Options.Create(new HackerNewsOptions
    {
        CacheTtlMinutes = 5
    });

    [Test]
    public async Task Single_flights_concurrent_story_id_cache_misses()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateCache(memoryCache);
        var callCount = 0;

        async Task<IReadOnlyList<int>> Fetcher(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref callCount);
            await Task.Delay(50, cancellationToken);
            return [10, 20, 30];
        }

        var results = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => cache.GetOrFetchStoryIdsAsync(Fetcher)));

        callCount.ShouldBe(1);
        results.All(result => result.SequenceEqual(new[] { 10, 20, 30 })).ShouldBeTrue();
    }

    [Test]
    public async Task Single_flights_concurrent_misses_for_the_same_story()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateCache(memoryCache);
        var callCount = 0;
        var expectedStory = StoryBuilder.Create(42).WithScore(500).Build();

        async Task<HackerNewsItem?> Fetcher(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref callCount);
            await Task.Delay(50, cancellationToken);
            return expectedStory;
        }

        var results = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => cache.GetOrFetchStoryAsync(42, Fetcher)));

        callCount.ShouldBe(1);
        results.All(story => story is not null && story.Id == expectedStory.Id).ShouldBeTrue();
    }

    [Test]
    public void ToResponse_returns_cached_instance_without_reallocating_dates()
    {
        var item = StoryBuilder.Create(77)
            .WithTime(1570887781)
            .WithScore(100)
            .Build();

        var first = item.ToResponse();
        var second = item.ToResponse();

        ReferenceEquals(first, second).ShouldBeTrue();
        ReferenceEquals(first.Time, second.Time).ShouldBeTrue();
        first.Time.ShouldStartWith("2019-10-12T");
    }

    private MemoryWithSemaphoreCache CreateCache(IMemoryCache memoryCache) => new(
        memoryCache,
        _options,
        TimeProvider.System,
        Mock.Of<ILogger<MemoryWithSemaphoreCache>>());
}
