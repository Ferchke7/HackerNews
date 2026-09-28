using System.Net;
using HackerNews.Application.Configuration;
using HackerNews.Infrastructure.Clients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;

namespace HackerNews.Tests.Unit;

[TestFixture]
public sealed class UpstreamRequestLimiterHandlerTests
{
    [Test]
    public async Task Acquires_a_new_permit_for_every_retry_attempt()
    {
        var limiter = new CountingLimiter();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHackerNewsRequestLimiter>(limiter);
        services.AddTransient<UpstreamRequestLimiterHandler>();
        var clientBuilder = services.AddHttpClient("retry-test")
            .ConfigurePrimaryHttpMessageHandler(() => new RetrySequenceHandler());
        clientBuilder.AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 2;
                options.Retry.BackoffType = DelayBackoffType.Constant;
                options.Retry.Delay = TimeSpan.FromMilliseconds(1);
                options.Retry.UseJitter = false;
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
            });
        clientBuilder.AddHttpMessageHandler<UpstreamRequestLimiterHandler>();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient("retry-test");

        var response = await client.GetAsync("https://example.test/story");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        limiter.Acquisitions.ShouldBe(3);
    }

    private sealed class RetrySequenceHandler : HttpMessageHandler
    {
        private int _attempt;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref _attempt);
            var statusCode = attempt < 3 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(string.Empty)
            });
        }
    }

    private sealed class CountingLimiter : IHackerNewsRequestLimiter
    {
        private int _acquisitions;

        public int Acquisitions => Volatile.Read(ref _acquisitions);

        public ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _acquisitions);
            return ValueTask.FromResult<IAsyncDisposable>(NoopLease.Instance);
        }
    }

    private sealed class NoopLease : IAsyncDisposable
    {
        public static NoopLease Instance { get; } = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
