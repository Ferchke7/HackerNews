using System.Text.Json.Serialization;

namespace HackerNews.Domain.Models;

/// <summary>
/// Raw item received from the Hacker News Firebase API (/v0/item/{id}.json).
/// </summary>
public sealed record HackerNewsItem
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("by")]
    public string By { get; init; } = string.Empty;

    [JsonPropertyName("time")]
    public long Time { get; init; }

    [JsonPropertyName("score")]
    public int Score { get; init; }

    [JsonPropertyName("descendants")]
    public int Descendants { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("deleted")]
    public bool Deleted { get; init; }

    [JsonPropertyName("dead")]
    public bool Dead { get; init; }
}
