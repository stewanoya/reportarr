using System.Net;
using System.Text;
using JellyfinReporter.MediaManager;
using Xunit;

namespace Reportarr.UnitTests.MediaManager;

public class ArrClientTests
{
    private static ArrServiceConfig Config(ArrServiceKind kind = ArrServiceKind.Sonarr) =>
        new(kind, "https://arr.example.test/", "test-api-key", 60_000);

    private static ArrClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHandler(responder);
        var http = new HttpClient(handler);
        return new ArrClient(http, Config());
    }

    [Fact]
    public async Task GetQueueAsync_SuccessResponse_ReturnsRecords()
    {
        var json = """
        {
          "page": 1,
          "pageSize": 200,
          "totalRecords": 2,
          "records": [
            {
              "id": 1,
              "downloadId": "abc",
              "title": "Some.Release",
              "status": "Downloading",
              "size": 1000,
              "sizeleft": 500,
              "timeleft": "00:10:00",
              "protocol": "Torrent"
            },
            {
              "id": 2,
              "downloadId": "def",
              "title": "Other.Release",
              "status": "Queued",
              "size": 2000,
              "sizeleft": 2000,
              "timeleft": null,
              "protocol": "Usenet"
            }
          ]
        }
        """;

        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var items = await client.GetQueueAsync();

        Assert.Equal(2, items.Count);
        Assert.Equal("Some.Release", items[0].Title);
        Assert.Equal("Downloading", items[0].Status);
        Assert.Equal(500m, items[0].SizeLeft);
        Assert.Equal(TimeSpan.FromMinutes(10), items[0].TimeLeft);
        Assert.Null(items[1].TimeLeft);
    }

    [Fact]
    public async Task GetQueueAsync_SendsApiKeyHeader()
    {
        HttpRequestMessage? captured = null;
        var client = CreateClient(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"records":[]}""", Encoding.UTF8, "application/json")
            };
        });

        await client.GetQueueAsync();

        Assert.NotNull(captured);
        Assert.True(captured!.Headers.Contains("X-Api-Key"));
        Assert.Equal("test-api-key", captured.Headers.GetValues("X-Api-Key").First());
    }

    [Fact]
    public async Task GetQueueAsync_HitsExpectedEndpointPath()
    {
        HttpRequestMessage? captured = null;
        var client = CreateClient(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"records":[]}""", Encoding.UTF8, "application/json")
            };
        });

        await client.GetQueueAsync();

        Assert.NotNull(captured);
        var uri = captured!.RequestUri!;
        Assert.StartsWith("https://arr.example.test/", uri.ToString());
        Assert.Contains("/api/v3/queue", uri.ToString());
        Assert.Contains("pageSize=200", uri.Query);
        Assert.Contains("sortKey=timeleft", uri.Query);
        Assert.Contains("includeSeries=true", uri.Query);
        Assert.Contains("includeEpisode=true", uri.Query);
        Assert.Contains("includeMovie=true", uri.Query);
    }

    [Fact]
    public async Task GetQueueAsync_Unauthorized_Throws()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"error":"unauthorized"}""")
        });

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetQueueAsync());
    }

    [Fact]
    public async Task GetQueueAsync_ServerError_Throws()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("boom")
        });

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetQueueAsync());
    }

    [Fact]
    public async Task GetQueueAsync_EmptyRecordsArray_ReturnsEmptyList()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"records":[]}""", Encoding.UTF8, "application/json")
        });

        var items = await client.GetQueueAsync();

        Assert.Empty(items);
    }

    [Fact]
    public async Task GetQueueAsync_NullRecords_ReturnsEmptyList()
    {
        // Defensive: a malformed payload with no 'records' field should not NRE.
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{}""", Encoding.UTF8, "application/json")
        });

        var items = await client.GetQueueAsync();

        Assert.Empty(items);
    }

    [Fact]
    public async Task GetQueueAsync_HandlesLowercaseSizeLeftKey()
    {
        // The 'sizeleft' field is lowercase by design in the Sonarr/Radarr API.
        // Ensure we bind it correctly (not via camelCase 'sizeLeft').
        var json = """
        {
          "records": [
            { "id": 1, "title": "x", "status": "Downloading", "size": 1000, "sizeleft": 250 }
          ]
        }
        """;

        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var items = await client.GetQueueAsync();

        Assert.Equal(250m, items[0].SizeLeft);
        Assert.Equal(1000m, items[0].Size);
    }

    [Fact]
    public async Task GetQueueAsync_RadarrMovieFieldsBind()
    {
        var json = """
        {
          "records": [
            {
              "id": 1,
              "title": "Some.Movie.Release",
              "status": "Downloading",
              "size": 5000,
              "sizeleft": 2500,
              "movieId": 42,
              "movie": { "title": "The Movie", "year": 2023 }
            }
          ]
        }
        """;

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var http = new HttpClient(handler);
        var client = new ArrClient(http, Config(ArrServiceKind.Radarr));

        var items = await client.GetQueueAsync();

        Assert.Equal(42, items[0].MovieId);
        Assert.Equal("The Movie", items[0].Movie?.Title);
        Assert.Equal(2023, items[0].Movie?.Year);
    }

    [Fact]
    public void Kind_ReflectsConfiguredService()
    {
        var sonarr = new ArrClient(new HttpClient(new StubHandler(_ => new HttpResponseMessage())), Config(ArrServiceKind.Sonarr));
        var radarr = new ArrClient(new HttpClient(new StubHandler(_ => new HttpResponseMessage())), Config(ArrServiceKind.Radarr));

        Assert.Equal(ArrServiceKind.Sonarr, sonarr.Kind);
        Assert.Equal(ArrServiceKind.Radarr, radarr.Kind);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
