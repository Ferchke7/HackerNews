using HackerNews.Domain.Models;

namespace HackerNews.Application.Common;

/// <summary>
/// Gateway interface to the external Hacker News API.
/// </summary>
public interface IHackerNewsApiClient
{
    /// <summary>
    /// Fetches the array of up to 200 best story IDs from Hacker News.
    /// </summary>
    Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken ct = default);

    /// <summary>
    /// Fetches the details of an individual item by its unique ID.
    /// Returns null if the item is not found or fails to load.
    /// </summary>
    Task<HackerNewsItem?> GetStoryAsync(int id, CancellationToken ct = default);
}