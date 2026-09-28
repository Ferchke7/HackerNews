namespace HackerNews.Infrastructure.Clients;

public interface IHackerNewsRequestLimiter
{
    ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default);
}
