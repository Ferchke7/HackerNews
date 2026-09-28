namespace HackerNews.Infrastructure.Clients;

public sealed class UpstreamRequestLimiterHandler(IHackerNewsRequestLimiter limiter) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await using var permit = await limiter.AcquireAsync(cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);

        try
        {
            // HttpClient buffers ResponseContentRead content after the handler chain
            // returns. Buffer it here so the lease covers the full upstream transfer.
            await response.Content.LoadIntoBufferAsync(cancellationToken);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}
