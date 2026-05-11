using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TermalogySkin.Services;

public sealed class GitHubStatsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _client;

    public GitHubStatsService(HttpClient? client = null)
    {
        _client = client ?? new HttpClient();
        _client.DefaultRequestHeaders.UserAgent.Clear();
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TermalogySkin", "0.1"));
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<GitHubStats> GetRepositoryStatsAsync(string owner, string repo, CancellationToken cancellationToken = default)
    {
        var repoUrl = $"https://api.github.com/repos/{owner}/{repo}";
        var releasesUrl = $"https://api.github.com/repos/{owner}/{repo}/releases";

        using var repoResponse = await _client.GetAsync(repoUrl, cancellationToken);
        repoResponse.EnsureSuccessStatusCode();

        var repoPayload = await repoResponse.Content.ReadAsStringAsync(cancellationToken);
        var repoInfo = JsonSerializer.Deserialize<RepositoryInfo>(repoPayload, JsonOptions)
                       ?? throw new InvalidOperationException("GitHub repo payload was empty.");

        using var releasesResponse = await _client.GetAsync(releasesUrl, cancellationToken);
        releasesResponse.EnsureSuccessStatusCode();

        var releasePayload = await releasesResponse.Content.ReadAsStringAsync(cancellationToken);
        var releases = JsonSerializer.Deserialize<List<ReleaseInfo>>(releasePayload, JsonOptions) ?? [];

        var totalDownloads = releases
            .SelectMany(release => release.Assets ?? [])
            .Sum(asset => asset.DownloadCount);

        return new GitHubStats(
            repoInfo.StargazersCount,
            repoInfo.ForksCount,
            repoInfo.WatchersCount,
            repoInfo.OpenIssuesCount,
            totalDownloads);
    }

    private sealed record RepositoryInfo(
        [property: JsonPropertyName("stargazers_count")] int StargazersCount,
        [property: JsonPropertyName("forks_count")] int ForksCount,
        [property: JsonPropertyName("subscribers_count")] int WatchersCount,
        [property: JsonPropertyName("open_issues_count")] int OpenIssuesCount);

    private sealed record ReleaseInfo(
        [property: JsonPropertyName("assets")] List<AssetInfo>? Assets);

    private sealed record AssetInfo(
        [property: JsonPropertyName("download_count")] int DownloadCount);
}

public readonly record struct GitHubStats(
    int Stars,
    int Forks,
    int Watchers,
    int OpenIssues,
    int TotalReleaseDownloads);
