using FluentResults;
using HackerNews.Domain.Stories;

namespace HackerNews.Api.Errors;

public static class RequestErrors
{
    public static Error InvalidStoryCount => StoryErrors.InvalidCount;
    public static Error MissingStoryCount => StoryErrors.MissingCount;
}
