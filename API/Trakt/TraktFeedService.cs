using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using API.Feeds;
using API.Trakt.Types;
using Contracts.Auth;
using Contracts.Feed;
using Contracts.Media;
using Domain.Auth;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;

namespace API.Trakt;

internal sealed class TraktFeedService(TraktClient client, IMediaService media, IApiKeyService apiKeys) : IFeedProvider
{
	private static readonly Error NotAPage = Error.From(
		"Paste a Trakt username, a trakt.tv watchlist or public list link, or a trending, popular or anticipated chart link."
	);

	public Task<Result<string>> NormalizeAsync(string input, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(TraktPages.Parse(input).ToResult(NotAPage).Map(TraktPages.Of).Map(page => page.Stored()));
	}

	public IAsyncEnumerable<MediaMatchResult> GetFeedItemsAsync(string identifier, CancellationToken cancellationToken = default)
	{
		return media.IdentifyWithDetailsAsync(RequestsAsync(identifier, cancellationToken), cancellationToken);
	}

	private async IAsyncEnumerable<IdentifyRequest> RequestsAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var clientId = await apiKeys
			.GetAsync(ApiKeyProvider.Trakt, cancellationToken)
			.GetOrThrow(_ => new InvalidOperationException("No Trakt client ID is configured. Add one in Settings."));
		foreach (var read in TraktPages.Reads(identifier))
		{
			var chart = TraktPages.ChartType(read);
			var entries = EntriesAsync(read, clientId, cancellationToken);
			await foreach (var entry in (TraktPages.IsChart(read) ? entries.Take(FeedPages.Titles) : entries).WithCancellation(cancellationToken))
			{
				yield return Request(entry, chart);
			}
		}
	}

	private static IdentifyRequest Request(TraktTypes.Item entry, Optional<MediaType> chart)
	{
		return Optional
			.Of(entry.Movie)
			.Map(movie => TraktTitle.Request(movie, MediaType.Movie))
			.Or(() => Optional.Of(entry.Show).Map(show => TraktTitle.Request(show, MediaType.Show)))
			.Or(() => chart.Map(type => TraktTitle.Request(Bare(entry), type)))
			.GetValueOr(() => throw new InvalidDataException("Trakt answered an entry that is no movie or show."));
	}

	private static TraktTypes.Title Bare(TraktTypes.Item entry) => new(entry.Name, entry.Year, entry.Ids, null, null, null, null, null, null, null);

	private async IAsyncEnumerable<TraktTypes.Item> EntriesAsync(
		string identifier,
		IApiKey clientId,
		[EnumeratorCancellation] CancellationToken cancellationToken
	)
	{
		var pages = 1;
		for (var page = 1; page <= pages; page++)
		{
			using var response = await client.GetAsync(TraktPages.Request(identifier, page), clientId, cancellationToken);
			Ensure(identifier, response);
			pages = Header(response, "X-Pagination-Page-Count").GetValueOr(1);
			var entries =
				await response.Content.ReadFromJsonAsync<List<TraktTypes.Item>>(cancellationToken) ?? throw new InvalidDataException("Trakt answered an empty page.");
			foreach (var entry in entries)
			{
				yield return entry;
			}
		}
	}

	private static void Ensure(string identifier, HttpResponseMessage response)
	{
		if (response.StatusCode is HttpStatusCode.NotFound)
		{
			throw new FeedIdentifierNotFoundException(identifier);
		}

		if (response.StatusCode is HttpStatusCode.Unauthorized && response.Headers.Contains("X-Private-User"))
		{
			throw new InvalidOperationException($"Trakt keeps the profile behind {identifier} private, so Sammlarr cannot read it.");
		}

		if (TraktClient.IsBlocked(response) || response.StatusCode is HttpStatusCode.TooManyRequests)
		{
			throw new RemoteUnavailableException(RemoteService.Trakt);
		}

		if (TraktClient.IsRejected(response))
		{
			throw new InvalidOperationException("Trakt refused the client ID. Check it in Settings › Providers.");
		}

		response.EnsureSuccessStatusCode();
	}

	private static Optional<int> Header(HttpResponseMessage response, string name)
	{
		IEnumerable<string> values = response.Headers.TryGetValues(name, out var found) ? found : [];
		return values
			.FirstOrNone(value => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _))
			.Map(value => int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture));
	}
}
