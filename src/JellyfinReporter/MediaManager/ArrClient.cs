using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace JellyfinReporter.MediaManager;

public sealed class ArrClient : IArrClient
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // includeSeries/includeEpisode for Sonarr; includeMovie for Radarr.
    // Both are harmless if the service doesn't use them.
    private const string _relativeUrl =
        "/api/v3/queue?page=1&pageSize=200&sortKey=timeleft&sortDirection=Ascending" +
        "&includeSeries=true&includeEpisode=true&includeMovie=true";

    private readonly HttpClient _http;
    private readonly ArrServiceConfig _config;

    public ArrClient(HttpClient http, ArrServiceConfig config)
    {
        _http = http;
        _config = config;
        _http.BaseAddress = new Uri(config.BaseUrl);
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _http.DefaultRequestHeaders.Add("X-Api-Key", config.ApiKey);
    }

    public ArrServiceKind Kind => _config.Kind;

    public async Task<IReadOnlyList<QueueItem>> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(_relativeUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var parsed = await JsonSerializer.DeserializeAsync<QueueResponse>(stream, _jsonOptions, cancellationToken);

        return parsed?.Records ?? [];
    }

    public async Task RemoveFromQueueAsync(int id, CancellationToken cancellationToken = default)
    {
        using var response = await _http.DeleteAsync(
            $"/api/v3/queue/{id}?removeFromClient=true&blocklist=true&skipRedownload=false",
            cancellationToken);

        // 404 = item already gone (completed or removed elsewhere). Treat as success.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return;

        response.EnsureSuccessStatusCode();
    }
}
