using System.ComponentModel.DataAnnotations;

namespace HackerNews.Application.Configuration;

/// <summary>
/// Service configuration. Caching and request limits are process-local; multiple
/// replicas require a distributed cache and limiter to share upstream budgets.
/// </summary>
public sealed class HackerNewsOptions
{
    public const string SectionName = "HackerNews";

    [Required]
    public string BaseUrl { get; init; } = "https://hacker-news.firebaseio.com/v0/";

    [Range(1, 60)]
    public int CacheTtlMinutes { get; init; } = 5;

    [Range(1, 100)]
    public int MaxDegreeOfParallelism { get; init; } = 20;

    [Range(1, 1_000)]
    public int MaxRequestsPerSecond { get; init; } = 30;

    [Range(1, 1_000)]
    public int RequestBurstCapacity { get; init; } = 30;

    [Range(1, 30)]
    public int UpstreamPermitWaitSeconds { get; init; } = 5;

    public bool EnableCacheWarmer { get; init; }

    [Range(1, 60)]
    public int WarmerIntervalMinutes { get; init; } = 2;
}
