using HackerNews.Domain.Models;

namespace HackerNews.Application.Common;

/// <summary>
/// Process-local cache abstraction for Hacker News IDs and story details.
/// </summary>
public interface IHackerNewsCache
{
    Task<IReadOnlyList<int>> GetOrFetchStoryIdsAsync(
        Func<CancellationToken, Task<IReadOnlyList<int>>> factory,
        CancellationToken ct = default);

    Task<HackerNewsItem?> GetOrFetchStoryAsync(
        int id,
        Func<CancellationToken, Task<HackerNewsItem?>> factory,
        CancellationToken ct = default);
}
