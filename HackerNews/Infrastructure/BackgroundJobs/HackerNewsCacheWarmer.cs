using HackerNews.Application.Configuration;
using HackerNews.Application.UseCases.Stories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HackerNews.Infrastructure.BackgroundJobs;

public sealed class HackerNewsCacheWarmer(
    IGetBestStoriesUseCase getBestStoriesUseCase,
    IOptions<HackerNewsOptions> options,
    ILogger<HackerNewsCacheWarmer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.EnableCacheWarmer)
        {
            logger.LogInformation("HackerNewsCacheWarmer is disabled by configuration.");
            return;
        }

        var interval = TimeSpan.FromMinutes(options.Value.WarmerIntervalMinutes);

        logger.LogInformation("HackerNewsCacheWarmer started with interval of {Minutes} minutes.", options.Value.WarmerIntervalMinutes);

        await RefreshCacheSafelyAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshCacheSafelyAsync(stoppingToken);
        }
    }

    private async Task RefreshCacheSafelyAsync(CancellationToken ct)
    {
        try
        {
            logger.LogDebug("Starting background refresh of top 200 Hacker News stories...");
            await getBestStoriesUseCase.HandleAsync(200, ct);
            logger.LogInformation("Hacker News cache refreshed successfully in background.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during scheduled Hacker News cache refresh.");
        }
    }
}
