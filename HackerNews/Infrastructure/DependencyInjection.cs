using HackerNews.Application.Common;
using HackerNews.Application.Configuration;
using HackerNews.Infrastructure.BackgroundJobs;
using HackerNews.Infrastructure.Caching;
using HackerNews.Infrastructure.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace HackerNews.Infrastructure;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddInfrastructure(IConfiguration configuration)
        {
            var optionsSection = configuration.GetSection(HackerNewsOptions.SectionName);
            var options = optionsSection.Get<HackerNewsOptions>() ?? new HackerNewsOptions();

            services.AddOptions<HackerNewsOptions>()
                .Bind(optionsSection)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Keep cache and upstream request budgets local to this process.
            services.AddMemoryCache();
            services.AddSingleton<IHackerNewsCache, MemoryWithSemaphoreCache>();
            services.AddSingleton<IHackerNewsRequestLimiter, LocalHackerNewsRequestLimiter>();

            services.AddTransient<UpstreamRequestLimiterHandler>();

            var baseUrl = options.BaseUrl;

            // Resilient HTTP Client with Polly v8 Standard Resilience Pipeline
            var httpClientBuilder = services.AddHttpClient<IHackerNewsApiClient, HackerNewsApiClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = 50,
                EnableMultipleHttp2Connections = true
            });

            httpClientBuilder.AddStandardResilienceHandler(resilience =>
            {
                resilience.Retry.MaxRetryAttempts = 3;
                resilience.Retry.BackoffType = DelayBackoffType.Exponential;
                resilience.Retry.UseJitter = true;
                resilience.Retry.Delay = TimeSpan.FromMilliseconds(500);
                resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            });

            // Registered after the resilience handler so every retry attempt consumes
            // a rate token and concurrency lease.
            httpClientBuilder.AddHttpMessageHandler<UpstreamRequestLimiterHandler>();

            services.AddHostedService<HackerNewsCacheWarmer>();

            return services;
        }
    }
}
