using FluentResults;
using HackerNews.Application.Common;
using HackerNews.Application.Configuration;
using HackerNews.Application.Mappers;
using HackerNews.Application.Models;
using HackerNews.Domain.Models;
using HackerNews.Domain.Stories;
using Microsoft.Extensions.Options;

namespace HackerNews.Application.UseCases.Stories;

public sealed class GetBestStoriesUseCase(
    IHackerNewsApiClient apiClient,
    IHackerNewsCache cache,
    IOptions<HackerNewsOptions> options) : IGetBestStoriesUseCase
{
    public async Task<Result<IReadOnlyList<StoryResponse>>> HandleAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return Result.Fail<IReadOnlyList<StoryResponse>>(StoryErrors.InvalidCount);
        }

        var storyIds = await cache.GetOrFetchStoryIdsAsync(apiClient.GetBestStoryIdsAsync, cancellationToken);
        if (storyIds.Count == 0)
        {
            return Result.Ok<IReadOnlyList<StoryResponse>>([]);
        }

        var rawStories = new HackerNewsItem?[storyIds.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, storyIds.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = options.Value.MaxDegreeOfParallelism,
                CancellationToken = cancellationToken
            },
            async (index, token) =>
            {
                var id = storyIds[index];
                rawStories[index] = await cache.GetOrFetchStoryAsync(
                    id,
                    ct => apiClient.GetStoryAsync(id, ct),
                    token);
            });

        var bestStories = rawStories
            .Where(item => item is { Deleted: false, Dead: false, Type: "story" or null or "" })
            .OrderByDescending(item => item!.Score)
            .Take(count)
            .Select(item => item!.ToResponse())
            .ToList();

        return Result.Ok<IReadOnlyList<StoryResponse>>(bestStories.AsReadOnly());
    }
}
