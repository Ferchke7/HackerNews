using FluentResults;
using HackerNews.Domain.Stories;

namespace HackerNews.Api.Errors;

public static class ApiErrors
{
    public static Error UpstreamUnavailable => StoryErrors.UpstreamUnavailable;
    public static Error Unexpected => StoryErrors.Unexpected;
}
