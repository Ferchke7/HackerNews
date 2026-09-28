namespace HackerNews.Application.Errors;

/// <summary>
/// Indicates that the external Hacker News dependency could not fulfill a request.
/// </summary>
public sealed class UpstreamUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
