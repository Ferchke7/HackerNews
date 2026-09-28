using FluentResults;
using HackerNews.Common.Errors;

namespace HackerNews.Domain.Stories;

public static class StoryErrors
{
    public static Error InvalidCount =>
        new Error("Parameter 'n' must be a positive integer greater than zero.")
            .WithErrorCode("S101");

    public static Error MissingCount =>
        new Error("Required parameter 'n' was not provided in the query string.")
            .WithErrorCode("S102");

    public static Error UpstreamUnavailable =>
        new Error("The external Hacker News API is temporarily unavailable.")
            .WithErrorCode("S201");

    public static Error Unexpected =>
        new Error("An unexpected error occurred.")
            .WithErrorCode("S500");
}
