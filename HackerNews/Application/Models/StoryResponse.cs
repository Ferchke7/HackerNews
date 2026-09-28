using System.Text.Json.Serialization;

namespace HackerNews.Application.Models;

/// <summary>
/// Output DTO for the Best Stories API response matching the exact coding test specification.
/// </summary>
public sealed record StoryResponse
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("uri")]
    public string? Uri { get; init; }

    [JsonPropertyName("postedBy")]
    public string PostedBy { get; init; } = string.Empty;

    [JsonPropertyName("time")]
    public string Time { get; init; } = string.Empty;

    [JsonPropertyName("score")]
    public int Score { get; init; }

    [JsonPropertyName("commentCount")]
    public int CommentCount { get; init; }
}
