using HackerNews.Api.Contracts;
using HackerNews.Application.Errors;
using Microsoft.AspNetCore.Diagnostics;

namespace HackerNews.Api.Errors;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        var isInvalidRequest = exception is BadHttpRequestException;
        var isUpstreamFailure = IsUpstreamFailure(exception);
        var error = isInvalidRequest
            ? RequestErrors.InvalidStoryCount
            : isUpstreamFailure
                ? ApiErrors.UpstreamUnavailable
                : ApiErrors.Unexpected;
        var statusCode = isInvalidRequest
            ? StatusCodes.Status400BadRequest
            : isUpstreamFailure
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status500InternalServerError;

        if (isInvalidRequest)
        {
            logger.LogInformation(exception, "The request contains an invalid story count.");
        }
        else
        {
            logger.LogError(exception, "Request failed with {ErrorCode}.", error.Metadata["ErrorCode"]);
        }
        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            ApiErrorResponse.From(error),
            cancellationToken);

        return true;
    }

    private static bool IsUpstreamFailure(Exception exception) => exception is UpstreamUnavailableException;
}
