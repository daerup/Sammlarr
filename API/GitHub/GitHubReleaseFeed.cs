using System.Text.Json;
using System.Text.Json.Serialization;
using Contracts.Updates;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;

namespace API.GitHub;

internal sealed class GitHubReleaseFeed(HttpClient http, ILogger<GitHubReleaseFeed> logger) : IReleaseFeed
{
	public async Task<Optional<PublishedRelease>> LatestAsync(string repository, CancellationToken cancellationToken = default)
	{
		try
		{
			using var response = await http.GetAsync($"repos/{repository}/releases/latest", cancellationToken);
			return response.IsSuccessStatusCode
				? Optional.Of(await response.Content.ReadFromJsonAsync<LatestRelease>(cancellationToken)).Map(release => Published(release))
				: new None();
		}
		catch (Exception ex)
			when (ex is HttpRequestException or JsonException or BrokenCircuitException or TimeoutRejectedException or RateLimiterRejectedException
				|| (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
			)
		{
			logger.LogInformation("The latest release of {Repository} could not be read: {Reason}", repository, ex.Message);
			return new None();
		}
	}

	private static Optional<PublishedRelease> Published(LatestRelease release)
	{
		return Optional.When(
			!string.IsNullOrWhiteSpace(release.TagName) && Uri.TryCreate(release.HtmlUrl, UriKind.Absolute, out _),
			() => new PublishedRelease(release.TagName!, new Uri(release.HtmlUrl!))
		);
	}

	private sealed record LatestRelease([property: JsonPropertyName("tag_name")] string? TagName, [property: JsonPropertyName("html_url")] string? HtmlUrl);
}
