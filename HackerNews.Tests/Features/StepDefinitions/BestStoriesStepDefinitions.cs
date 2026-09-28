using System.Net.Http.Json;
using System.Text.Json;
using HackerNews.Application.Common;
using HackerNews.Application.Errors;
using HackerNews.Application.Models;
using HackerNews.Domain.Models;
using HackerNews.Tests.Builders;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Reqnroll;
using Shouldly;

namespace HackerNews.Tests.Features.StepDefinitions;

[Binding]
public class BestStoriesStepDefinitions
{
    private readonly Mock<IHackerNewsApiClient> _mockApiClient = new();
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private HttpResponseMessage _response = null!;

    [BeforeScenario]
    public void BeforeScenario()
    {
        _mockApiClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["HackerNews:EnableCacheWarmer"] = "false"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHackerNewsApiClient>();
                services.AddSingleton(_mockApiClient.Object);
            });
        });
        _client = _factory.CreateClient();
    }

    [AfterScenario]
    public void AfterScenario()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Given(@"the external Hacker News API contains stories:")]
    public void GivenTheExternalHackerNewsApiContainsStories(Table table)
    {
        var items = new List<HackerNewsItem>();
        foreach (var row in table.Rows)
        {
            var item = StoryBuilder.Create(int.Parse(row["Id"]))
                .WithTitle(row["Title"])
                .WithScore(int.Parse(row["Score"]))
                .WithAuthor(row["PostedBy"])
                .Build();

            items.Add(item);
            _mockApiClient.Setup(c => c.GetStoryAsync(item.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);
        }

        _mockApiClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(items.Select(x => x.Id).ToList());
    }

    [Given(@"the external Hacker News API contains complex items:")]
    public void GivenTheExternalHackerNewsApiContainsComplexItems(Table table)
    {
        var items = new List<HackerNewsItem>();
        foreach (var row in table.Rows)
        {
            var builder = StoryBuilder.Create(int.Parse(row["Id"]))
                .WithTitle(row["Title"])
                .WithScore(int.Parse(row["Score"]))
                .WithAuthor(row["PostedBy"])
                .WithType(row["Type"]);

            if (bool.Parse(row["Deleted"])) builder.Deleted();
            if (bool.Parse(row["Dead"])) builder.Dead();

            var item = builder.Build();
            items.Add(item);

            _mockApiClient.Setup(c => c.GetStoryAsync(item.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);
        }

        _mockApiClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(items.Select(x => x.Id).ToList());
    }

    [Given(@"the external Hacker News API contains an Ask HN story with id (.*) and score (.*)")]
    public void GivenTheExternalHackerNewsApiContainsAnAskHnStory(int id, int score)
    {
        var item = StoryBuilder.Create(id)
            .WithTitle("Ask HN: Favorite C# 12 Features?")
            .WithUrl(null)
            .WithScore(score)
            .Build();

        _mockApiClient.Setup(c => c.GetStoryAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        _mockApiClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([id]);
    }

    [Given(@"the external Hacker News API contains a sample story matching the specification:")]
    public void GivenTheExternalHackerNewsApiContainsSampleStory(Table table)
    {
        var dict = table.Rows.ToDictionary(r => r["Field"], r => r["Value"]);
        var item = StoryBuilder.Create(1)
            .WithTitle(dict["Title"])
            .WithUrl(dict["Uri"])
            .WithAuthor(dict["PostedBy"])
            .WithTime(long.Parse(dict["Time"]))
            .WithScore(int.Parse(dict["Score"]))
            .WithComments(int.Parse(dict["CommentCount"]))
            .Build();

        _mockApiClient.Setup(c => c.GetStoryAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        _mockApiClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([1]);
    }

    [Given(@"the external Hacker News API is unavailable")]
    public void GivenTheExternalHackerNewsApiIsUnavailable()
    {
        _mockApiClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UpstreamUnavailableException("upstream unavailable"));
    }

    [When(@"I request the best (.*) stories")]
    public async Task WhenIRequestTheBestStories(int n)
    {
        _response = await _client.GetAsync($"/api/stories/best?n={n}");
    }

    [When(@"I request best stories without parameter N")]
    public async Task WhenIRequestBestStoriesWithoutParameterN()
    {
        _response = await _client.GetAsync("/api/stories/best");
    }

    [Then(@"the response status code should be (.*)")]
    public void ThenTheResponseStatusCodeShouldBe(int expectedStatusCode)
    {
        ((int)_response.StatusCode).ShouldBe(expectedStatusCode);
    }

    [Then(@"the error response should have code ""(.*)""")]
    public async Task ThenTheErrorResponseShouldHaveCode(string expectedCode)
    {
        using var doc = JsonDocument.Parse(await _response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().ShouldBe(expectedCode);
    }

    [Then(@"the returned stories should have scores in order:")]
    public async Task ThenTheReturnedStoriesShouldHaveScoresInOrder(Table table)
    {
        var stories = await _response.Content.ReadFromJsonAsync<StoryResponse[]>();
        stories.ShouldNotBeNull();

        var expectedScores = table.Rows.Select(r => int.Parse(r["Score"])).ToArray();
        var actualScores = stories!.Select(s => s.Score).ToArray();

        actualScores.ShouldBe(expectedScores);
    }

    [Then(@"exactly (.*) stories should be returned")]
    public async Task ThenExactlyStoriesShouldBeReturned(int expectedCount)
    {
        var stories = await _response.Content.ReadFromJsonAsync<StoryResponse[]>();
        stories.ShouldNotBeNull();
        stories!.Length.ShouldBe(expectedCount);
    }

    [Then(@"story (.*) should have null uri")]
    public async Task ThenStoryShouldHaveNullUri(int index)
    {
        var stories = await _response.Content.ReadFromJsonAsync<StoryResponse[]>();
        stories.ShouldNotBeNull();
        stories![index].Uri.ShouldBeNull();
    }

    [Then(@"story (.*) should strictly match the specification fields")]
    public async Task ThenStoryShouldStrictlyMatchSpecificationFields(int index)
    {
        var stories = await _response.Content.ReadFromJsonAsync<StoryResponse[]>();
        stories.ShouldNotBeNull();
        var s = stories![index];

        s.Title.ShouldBe("A uBlock Origin update was rejected from the Chrome Web Store");
        s.Uri.ShouldBe("https://github.com/uBlockOrigin/uBlock-issues/issues/745");
        s.PostedBy.ShouldBe("ismaildonmez");
        s.Score.ShouldBe(1716);
        s.CommentCount.ShouldBe(572);
        s.Time.ShouldStartWith("2019-10-12T");
    }
}
