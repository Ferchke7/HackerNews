using HackerNews.Application.Models;

namespace HackerNews.Api.Endpoints.Stories;

public static class StoriesEndpoints
{
    extension(IEndpointRouteBuilder builder)
    {
        public void MapStoriesEndpoints()
        {
            var group = builder.MapGroup("api/stories")
                .WithTags("Stories");

            group.MapGet("best", GetBestStoriesHandler.Handle)
                .WithName("GetBestStories")
                .WithSummary("Get top N best stories from Hacker News")
                .WithDescription("Retrieves the details of the top N stories from Hacker News, sorted in descending order of score. Responses are cached and requests are throttled to ensure system resilience.")
                .CacheOutput("BestStories")
                .Produces<IReadOnlyList<StoryResponse>>()
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status503ServiceUnavailable)
                .Produces(StatusCodes.Status500InternalServerError);
        }
    }
}
