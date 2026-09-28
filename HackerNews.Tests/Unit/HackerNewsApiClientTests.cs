using System.Net;
using HackerNews.Application.Errors;
using HackerNews.Infrastructure.Clients;
using Shouldly;

namespace HackerNews.Tests.Unit;

[TestFixture]
public sealed class HackerNewsApiClientTests
{
    [Test]
    public async Task Converts_upstream_http_failures_to_application_exception()
    {
        using var httpClient = CreateClient((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var apiClient = new HackerNewsApiClient(httpClient);

        var exception = await Should.ThrowAsync<UpstreamUnavailableException>(async () =>
        {
            await apiClient.GetBestStoryIdsAsync();
        });

        exception.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Test]
    public async Task Caller_cancellation_is_preserved()
    {
        using var httpClient = CreateClient((_, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var apiClient = new HackerNewsApiClient(httpClient);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await apiClient.GetBestStoryIdsAsync(cancellation.Token);
        });
    }

    [Test]
    public async Task Missing_story_returns_null()
    {
        using var httpClient = CreateClient((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.NotFound)));
        var apiClient = new HackerNewsApiClient(httpClient);

        var story = await apiClient.GetStoryAsync(123);

        story.ShouldBeNull();
    }

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new DelegateHttpMessageHandler(send))
        {
            BaseAddress = new Uri("https://hacker-news.test/v0/")
        };

    private sealed class DelegateHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
