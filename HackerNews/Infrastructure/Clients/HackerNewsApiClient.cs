using System.Net.Http.Json;
using System.Text.Json;
using HackerNews.Application.Common;
using HackerNews.Application.Errors;
using HackerNews.Domain.Models;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace HackerNews.Infrastructure.Clients;

public sealed class HackerNewsApiClient(
    HttpClient httpClient) : IHackerNewsApiClient
{
    public Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken ct = default) =>
        ExecuteUpstreamRequestAsync<IReadOnlyList<int>>(async () =>
        {
            using var response = await httpClient.GetAsync(
                "beststories.json",
                HttpCompletionOption.ResponseContentRead,
                ct);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<int[]>(ct)
                ?? throw new InvalidDataException("Hacker News returned a null best-story ID list.");
        }, ct);

    public Task<HackerNewsItem?> GetStoryAsync(int id, CancellationToken ct = default) =>
        ExecuteUpstreamRequestAsync(async () =>
        {
            using var response = await httpClient.GetAsync(
                $"item/{id}.json",
                HttpCompletionOption.ResponseContentRead,
                ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<HackerNewsItem>(ct);
        }, ct);

    private static async Task<TResult> ExecuteUpstreamRequestAsync<TResult>(
        Func<Task<TResult>> request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await request();
        }
        catch (UpstreamUnavailableException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsUpstreamFailure(exception))
        {
            throw new UpstreamUnavailableException(
                "The external Hacker News API is temporarily unavailable.",
                exception);
        }
    }

    private static bool IsUpstreamFailure(Exception exception) => exception is
        HttpRequestException or
        JsonException or
        InvalidDataException or
        IOException or
        TimeoutException or
        TimeoutRejectedException or
        BrokenCircuitException or
        OperationCanceledException;
}
