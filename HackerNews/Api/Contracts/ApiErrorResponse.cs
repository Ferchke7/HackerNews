using FluentResults;
using HackerNews.Api.Errors;

namespace HackerNews.Api.Contracts;

public sealed record ApiErrorResponse(string Error, string Code)
{
    public static ApiErrorResponse From(IError error) => new(
        error.Message,
        error.Metadata.GetValueOrDefault("ErrorCode")?.ToString()
            ?? ApiErrors.Unexpected.Metadata["ErrorCode"].ToString()!);
}
