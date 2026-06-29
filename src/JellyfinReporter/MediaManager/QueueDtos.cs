using System.Text.Json;
using System.Text.Json.Serialization;

namespace JellyfinReporter.MediaManager;

public class QueueResponse
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalRecords")]
    public int TotalRecords { get; set; }

    [JsonPropertyName("records")]
    public List<QueueItem> Records { get; set; } = [];
}

public class QueueItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("downloadId")]
    public string? DownloadId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("trackedDownloadStatus")]
    public string? TrackedDownloadStatus { get; set; }

    [JsonPropertyName("trackedDownloadState")]
    public string? TrackedDownloadState { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("statusMessages")]
    public List<StatusMessage>? StatusMessages { get; set; }

    [JsonPropertyName("size")]
    public decimal Size { get; set; }

    // NOTE: 'sizeleft' is deliberately lowercase in the Sonarr/Radarr API
    // (legacy collision in their QueueResource.cs). Do NOT change to camelCase.
    [JsonPropertyName("sizeleft")]
    public decimal SizeLeft { get; set; }

    // NOTE: 'timeleft' is deliberately lowercase in the API (same legacy reason).
    [JsonPropertyName("timeleft")]
    [JsonConverter(typeof(TimeLeftConverter))]
    public TimeSpan? TimeLeft { get; set; }

    [JsonPropertyName("estimatedCompletionTime")]
    public DateTime? EstimatedCompletionTime { get; set; }

    [JsonPropertyName("added")]
    public DateTime? Added { get; set; }

    [JsonPropertyName("protocol")]
    public string? Protocol { get; set; }

    [JsonPropertyName("downloadClient")]
    public string? DownloadClient { get; set; }

    [JsonPropertyName("indexer")]
    public string? Indexer { get; set; }

    // Sonarr-specific embedded resources
    [JsonPropertyName("seriesId")]
    public int? SeriesId { get; set; }

    [JsonPropertyName("episodeId")]
    public int? EpisodeId { get; set; }

    [JsonPropertyName("seasonNumber")]
    public int? SeasonNumber { get; set; }

    [JsonPropertyName("series")]
    public SeriesSummary? Series { get; set; }

    [JsonPropertyName("episode")]
    public EpisodeSummary? Episode { get; set; }

    // Radarr-specific embedded resource
    [JsonPropertyName("movieId")]
    public int? MovieId { get; set; }

    [JsonPropertyName("movie")]
    public MovieSummary? Movie { get; set; }
}

public class StatusMessage
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<string> Messages { get; set; } = [];
}

public class SeriesSummary
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}

public class EpisodeSummary
{
    [JsonPropertyName("episodeNumber")]
    public int EpisodeNumber { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("airDate")]
    public string? AirDate { get; set; }
}

public class MovieSummary
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("year")]
    public int Year { get; set; }
}
