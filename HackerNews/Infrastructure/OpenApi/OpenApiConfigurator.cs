using Microsoft.AspNetCore.OpenApi;

namespace HackerNews.Infrastructure.OpenApi;

public static class OpenApiConfigurator
{
    public static void Configure(OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, context, ct) =>
        {
            document.Info.Title = "Hacker News Best Stories API";
            document.Info.Version = "v1";
            document.Info.Description = "High-performance RESTful API that retrieves the top N best stories from Hacker News, sorted by score in descending order.";
            return Task.CompletedTask;
        });
    }
}
