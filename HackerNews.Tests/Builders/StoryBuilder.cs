using HackerNews.Domain.Models;

namespace HackerNews.Tests.Builders;

public sealed class StoryBuilder
{
    private int _id = 1;
    private string _title = "Test Story";
    private string? _url = "https://news.ycombinator.com";
    private string _by = "author";
    private long _time = 1570887781;
    private int _score = 100;
    private int _descendants = 10;
    private string _type = "story";
    private bool _deleted;
    private bool _dead;

    private StoryBuilder(int id)
    {
        _id = id;
        _title = $"Story {id}";
    }

    public static StoryBuilder Create(int id) => new(id);

    public StoryBuilder WithScore(int score)
    {
        _score = score;
        return this;
    }

    public StoryBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public StoryBuilder WithUrl(string? url)
    {
        _url = url;
        return this;
    }

    public StoryBuilder WithAuthor(string author)
    {
        _by = author;
        return this;
    }

    public StoryBuilder WithTime(long unixTime)
    {
        _time = unixTime;
        return this;
    }

    public StoryBuilder WithComments(int count)
    {
        _descendants = count;
        return this;
    }

    public StoryBuilder WithType(string type)
    {
        _type = type;
        return this;
    }

    public StoryBuilder Deleted()
    {
        _deleted = true;
        return this;
    }

    public StoryBuilder Dead()
    {
        _dead = true;
        return this;
    }

    public HackerNewsItem Build() => new()
    {
        Id = _id,
        Title = _title,
        Url = _url,
        By = _by,
        Time = _time,
        Score = _score,
        Descendants = _descendants,
        Type = _type,
        Deleted = _deleted,
        Dead = _dead
    };
}
