namespace JellyfinReporter;

public class JellyfinClient : IJellyfinClient
{
    private readonly HttpClient _http;
    private readonly AppSettings _settings;

    public JellyfinClient(HttpClient http, AppSettings settings)
    {
        _http = http;
        _settings = settings;
        _http.BaseAddress = new Uri(settings.HealthCheck.BaseUrl);
    }

    public async Task<bool> CheckServerHealthAsync()
    {
        var response = await _http.GetAsync(_settings.HealthCheck.Endpoint);

        return response.IsSuccessStatusCode;
    }
}