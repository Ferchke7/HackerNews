using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HackerNews.Application.Common;
using HackerNews.Application.Errors;
using HackerNews.Application.Models;
using HackerNews.Domain.Models;
using HackerNews.Tests.Builders;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace HackerNews.Tests.Integration;

[TestFixture]
public class BestStoriesEndpointTests
{
    private Mock<IHackerNewsApiClient> _apiClient = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _apiClient = new Mock<IHackerNewsApiClient>();
        _apiClient.Setup(client => client.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _factory = CreateFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task GetBestStories_WithValidN_ReturnsSortedJsonArray()
    {
        _apiClient.Setup(client => client.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([101, 102, 103]);
        _apiClient.Setup(client => client.GetStoryAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StoryBuilder.Create(101).WithScore(50).Build());
        _apiClient.Setup(client => client.GetStoryAsync(102, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StoryBuilder.Create(102).WithScore(800).Build());
        _apiClient.Setup(client => client.GetStoryAsync(103, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StoryBuilder.Create(103).WithScore(300).Build());

        var response = await _client.GetAsync("/api/stories/best?n=2");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        var stories = await response.Content.ReadFromJsonAsync<StoryResponse[]>();
        stories!.Select(story => story.Score).ShouldBe([800, 300]);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-50)]
    public async Task GetBestStories_WithNonPositiveN_ReturnsBadRequestWithErrorCode(int invalidN)
    {
        var response = await _client.GetAsync($"/api/stories/best?n={invalidN}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().ShouldBe("S101");
    }

    [Test]
    public async Task GetBestStories_WithoutNParameter_ReturnsBadRequestWithErrorCode()
    {
        var response = await _client.GetAsync("/api/stories/best");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().ShouldBe("S102");
    }

    [Test]
    public async Task GetBestStories_WithNonIntegerN_ReturnsBadRequestWithErrorCode()
    {
        var response = await _client.GetAsync("/api/stories/best?n=not-a-number");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().ShouldBe("S101");
    }

    [Test]
    public async Task GetBestStories_WhenUpstreamFails_ReturnsServiceUnavailableWithErrorCode()
    {
        _apiClient.Setup(client => client.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UpstreamUnavailableException("upstream unavailable"));

        var response = await _client.GetAsync("/api/stories/best?n=5");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().ShouldBe("S201");
    }

    [Test]
    public async Task GetBestStories_WhenARequiredStoryFetchFails_ReturnsServiceUnavailable()
    {
        _apiClient.Setup(client => client.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([101]);
        _apiClient.Setup(client => client.GetStoryAsync(101, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UpstreamUnavailableException("story unavailable"));

        var response = await _client.GetAsync("/api/stories/best?n=5");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().ShouldBe("S201");
    }

    [Test]
    public async Task GetBestStories_WhenAnUnexpectedFailureOccurs_ReturnsInternalServerError()
    {
        _apiClient.Setup(client => client.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("unexpected failure"));

        var response = await _client.GetAsync("/api/stories/best?n=5");

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().ShouldBe("S500");
        doc.RootElement.GetProperty("error").GetString().ShouldBe("An unexpected error occurred.");
    }

    [Test]
    public async Task OpenApi_Endpoint_ReturnsValidJsonSpec()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Hacker News Best Stories API");
    }

    [Test]
    public async Task HealthCheck_ReturnsHealthyStatus()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["HackerNews:EnableCacheWarmer"] = "false"
                };

                configuration.AddInMemoryCollection(values);
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHackerNewsApiClient>();
                services.AddSingleton(_apiClient.Object);
            });
        });
}
