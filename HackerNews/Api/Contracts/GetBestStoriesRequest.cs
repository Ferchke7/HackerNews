using Microsoft.AspNetCore.Mvc;

namespace HackerNews.Api.Contracts;

public sealed record GetBestStoriesRequest
{
    [FromQuery(Name = "n")]
    public int? N { get; init; }
}
