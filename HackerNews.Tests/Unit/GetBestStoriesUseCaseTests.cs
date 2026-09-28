using FluentResults;
using HackerNews.Application.Common;
using HackerNews.Application.Configuration;
using HackerNews.Application.Models;
using HackerNews.Application.UseCases.Stories;
using HackerNews.Domain.Models;
using HackerNews.Tests.Builders;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace HackerNews.Tests.Unit;

[TestFixture]
public class GetBestStoriesUseCaseTests
{
    private Mock<IHackerNewsApiClient> _apiClient = null!;
    private IMemoryCache _cache = null!;
    private GetBestStoriesUseCase _sut = null!;
    private readonly IOptions<HackerNewsOptions> _options = Options.Create(new HackerNewsOptions
    {
        CacheTtlMinutes = 5,
        MaxDegreeOfParallelism = 10
    });

    [SetUp]
    public void SetUp()
    {
        _apiClient = new Mock<IHackerNewsApiClient>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _sut = CreateSut();
    }

    [TearDown]
    public void TearDown() => _cache.Dispose();

    private GetBestStoriesUseCase CreateSut() => new(
        _apiClient.Object,
        new HackerNews.Infrastructure.Caching.MemoryWithSemaphoreCache(
            _cache,
            _options,
            TimeProvider.System,
            Mock.Of<ILogger<HackerNews.Infrastructure.Caching.MemoryWithSemaphoreCache>>()),
        _options);

    private Task<Result<IReadOnlyList<StoryResponse>>> Act(int count = 3, CancellationToken ct = default) =>
        _sut.HandleAsync(count, ct);

    private void SetupStories(params (int id, int score)[] stories)
    {
        var ids = stories.Select(s => s.id).ToList();
        _apiClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids);

        foreach (var (id, score) in stories)
        {
            _apiClient.Setup(c => c.GetStoryAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(StoryBuilder.Create(id).WithScore(score).Build());
        }
    }

    private void SetupStories(params HackerNewsItem?[] stories)
    {
        var ids = stories.Select((s, i) => s?.Id ?? (i + 1000)).ToList();
        _apiClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids);

        for (var i = 0; i < stories.Length; i++)
        {
            var item = stories[i];
            var id = item?.Id ?? (i + 1000);
            _apiClient.Setup(c => c.GetStoryAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);
        }
    }

    [Test]
    public async Task Returns_stories_in_descending_score_order()
    {
        SetupStories((1, 100), (2, 350), (3, 200));

        var result = await Act(count: 3);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(s => s.Score).ShouldBe([350, 200, 100]);
    }

    [Test]
    public async Task Respects_requested_story_count()
    {
        SetupStories((1, 100), (2, 200), (3, 300));

        var result = await Act(count: 2);

        result.Value.Count.ShouldBe(2);
    }

    [Test]
    public async Task Filters_out_deleted_and_dead_stories()
    {
        SetupStories(
            StoryBuilder.Create(1).WithScore(100).Build(),
            StoryBuilder.Create(2).WithScore(500).Deleted().Build(),
            StoryBuilder.Create(3).WithScore(400).Dead().Build(),
            StoryBuilder.Create(4).WithScore(300).Build());

        var result = await Act(count: 4);

        result.Value.Select(s => s.Score).ShouldBe([300, 100]);
    }

    [Test]
    public async Task Filters_out_non_story_types_like_jobs_and_polls()
    {
        SetupStories(
            StoryBuilder.Create(1).WithType("job").WithScore(800).Build(),
            StoryBuilder.Create(2).WithType("poll").WithScore(900).Build(),
            StoryBuilder.Create(3).WithType("story").WithScore(400).Build());

        var result = await Act(count: 3);

        result.Value.Select(s => s.Score).ShouldBe([400]);
    }

    [Test]
    public async Task Handles_null_story_gracefully_when_individual_item_fails_to_load()
    {
        SetupStories(StoryBuilder.Create(1).WithScore(250).Build(), null);

        var result = await Act(count: 2);

        result.Value.Select(s => s.Score).ShouldBe([250]);
    }

    [Test]
    public async Task Maps_story_without_url_to_null_uri()
    {
        SetupStories(StoryBuilder.Create(10).WithUrl(null).Build());

        var result = await Act(count: 1);

        result.Value[0].Uri.ShouldBeNull();
    }

    [Test]
    public async Task Returns_empty_result_when_upstream_api_returns_no_story_ids()
    {
        _apiClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await Act();

        result.Value.ShouldBeEmpty();
    }

    [Test]
    public async Task Maps_fields_and_formats_timestamp_to_iso8601()
    {
        var story = StoryBuilder.Create(42)
            .WithTitle("Trading Systems")
            .WithUrl("https://example.com/trading")
            .WithAuthor("alex_trader")
            .WithTime(1570887781)
            .WithScore(1716)
            .WithComments(572)
            .Build();
        SetupStories(story);

        var result = await Act(count: 1);

        var item = result.Value[0];
        item.Title.ShouldBe("Trading Systems");
        item.Uri.ShouldBe("https://example.com/trading");
        item.PostedBy.ShouldBe("alex_trader");
        item.Score.ShouldBe(1716);
        item.CommentCount.ShouldBe(572);
        item.Time.ShouldStartWith("2019-10-12T");
    }

    [Test]
    public async Task Hits_cache_on_subsequent_calls_without_calling_api()
    {
        SetupStories((10, 100), (20, 200));

        await Act(count: 2);
        await Act(count: 2);

        _apiClient.Verify(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Does_not_serve_expired_cache_entries_when_upstream_fails()
    {
        var expired = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        _cache.Set("hn_best_story_ids", (IReadOnlyList<int>)[1], expired);
        _cache.Set("hn_story_1", StoryBuilder.Create(1).WithScore(100).Build(), expired);
        _apiClient.Setup(client => client.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([1]);
        _apiClient.Setup(client => client.GetStoryAsync(1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("upstream unavailable"));

        await Should.ThrowAsync<HttpRequestException>(async () =>
        {
            _ = await Act(1);
        });

        _apiClient.Verify(client => client.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()), Times.Once);
        _apiClient.Verify(client => client.GetStoryAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Protects_against_cache_stampede_under_concurrent_requests()
    {
        SetupStories((1, 100), (2, 200));

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Act(2)));

        _apiClient.Verify(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()), Times.Once);
        _apiClient.Verify(c => c.GetStoryAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        _apiClient.Verify(c => c.GetStoryAsync(2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public Task Propagates_upstream_error_when_story_details_cannot_be_loaded()
    {
        _apiClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([1]);
        _apiClient.Setup(c => c.GetStoryAsync(1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("upstream unavailable"));

        return Should.ThrowAsync<HttpRequestException>(async () =>
        {
            _ = await Act(1);
        });
    }

    [Test]
    public async Task Propagates_request_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _apiClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => Task.FromCanceled<IReadOnlyList<int>>(ct));

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            _ = await Act(1, cancellation.Token);
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    public async Task Returns_failure_with_error_code_for_non_positive_count(int count)
    {
        var result = await Act(count);

        result.ShouldHaveErrorCode("S101");
    }
}
