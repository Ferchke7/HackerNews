using System.Runtime.CompilerServices;
using HackerNews.Application.Models;
using HackerNews.Domain.Models;

namespace HackerNews.Application.Mappers;

public static class StoryMapper
{
    private static readonly ConditionalWeakTable<HackerNewsItem, StoryResponse> Cache = new();

    public static StoryResponse ToResponse(this HackerNewsItem item) =>
        Cache.GetValue(item, static i => new StoryResponse
        {
            Title = i.Title,
            Uri = i.Url,
            PostedBy = i.By,
            Time = DateTimeOffset.FromUnixTimeSeconds(i.Time).ToString("yyyy-MM-ddTHH:mm:sszzz"),
            Score = i.Score,
            CommentCount = i.Descendants
        });
}
