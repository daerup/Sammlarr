using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using API.Http;
using API.Mdblist.Types;
using Contracts.Auth;
using Contracts.Feed;
using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Auth;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;
using Observability.Feed;

namespace API.Mdblist;

internal sealed class MdblistService(MdblistClient client, IMediaService media, IApiKeyService apiKeys, MdblistTitles titles) : IFeedProvider
{
	private const int PageSize = 1000;
	private const string HasMoreHeader = "X-Has-More";

	private static readonly Error NotAList = Error.From("Paste the list's link from mdblist.com, its owner/list, or its number.");

	private static readonly Error NoKey = Error.From("Add an MDBList key in Settings › Providers to look a list up by its number, or paste the list's link.");

	public Task<Result<string>> NormalizeAsync(string input, CancellationToken cancellationToken = default)
	{
		return MdblistLists
			.Parse(input)
			.Match(
				list =>
					long.TryParse(list, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
						? PathOfAsync(number, cancellationToken)
						: Task.FromResult<Result<string>>(Success.Of(list)),
				() => Task.FromResult<Result<string>>(NotAList)
			);
	}

	private async Task<Result<string>> PathOfAsync(long number, CancellationToken cancellationToken)
	{
		return await apiKeys.GetAsync(ApiKeyProvider.Mdblist, cancellationToken).MapError(_ => NoKey).Map(key => FindListAsync(number, key, cancellationToken));
	}

	private async Task<Result<string>> FindListAsync(long number, IApiKey apiKey, CancellationToken cancellationToken)
	{
		var endpoint = string.Format(CultureInfo.InvariantCulture, MdblistEndpoints.List, number);
		try
		{
			using var response = await client.SendAsync(HttpMethod.Get, endpoint, apiKey, cancellationToken);
			return await ListPathAsync(number, response, cancellationToken);
		}
		catch (Exception ex) when (ex is RemoteUnavailableException or HttpRequestException)
		{
			return NotReached(number, ex);
		}
	}

	private static async Task<Result<string>> ListPathAsync(long number, HttpResponseMessage response, CancellationToken cancellationToken)
	{
		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			return NoList(number);
		}

		if (!response.IsSuccessStatusCode)
		{
			var refused = response.StatusCode.Unanswered(RemoteService.Mdblist, $"MDBList refused to look up list {number} ({(int)response.StatusCode}).");
			return refused.CausedBy<RemoteUnavailableException>().Match(unavailable => NotReached(number, unavailable), () => refused);
		}

		var lists = await response.Content.ReadFromJsonAsync<IReadOnlyList<MdblistTypes.Lists.Info>>(cancellationToken);
		return lists!.FirstOrNone().Map(list => $"{list.UserName}/{list.Slug}").ToResult(NoList(number));
	}

	private static Error NoList(long number) => Error.From($"MDBList has no list {number}.");

	private static Error NotReached(long number, Exception cause)
	{
		var unavailable = cause as RemoteUnavailableException ?? new RemoteUnavailableException(RemoteService.Mdblist, sent: true, cause);
		return Error.From($"MDBList didn't answer, so {number} can't be looked up yet. Paste the list's link instead; it needs no request.", unavailable);
	}

	public IAsyncEnumerable<Listed<MediaMatchResult>> GetFeedItemsAsync(string identifier, FieldNeeds needs, CancellationToken cancellationToken = default)
	{
		return media.LoadListAsync(EntriesAsync(identifier, cancellationToken), needs, cancellationToken);
	}

	private async IAsyncEnumerable<ListTitle> EntriesAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var page in FetchPagesAsync(identifier, cancellationToken))
		{
			foreach (var item in page.unmatched)
			{
				yield return BuildUnmatched(item);
			}

			var requests = page
				.shows.Select(FeedItemResolution.Show)
				.Concat(page.movies.Select(FeedItemResolution.Movie))
				.Select(pair => new IdentifyRequest(MdblistIds.Of(pair.ListItem.ids, pair.MediaType), BuildHints(pair)) { Sources = [RemoteService.Mdblist] })
				.ToList();
			titles.Prefetch(requests.Select(request => request.Candidates));
			foreach (var request in requests)
			{
				yield return request;
			}
		}
	}

	private async IAsyncEnumerable<MdblistTypes.ListItems.Root> FetchPagesAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var offset = 0;
		var hasMore = true;
		while (hasMore)
		{
			(var page, hasMore) = await SourcePages.ReadAsync(offset / PageSize + 1, () => FetchPageAsync(identifier, offset, cancellationToken));
			FeedRunSpans.SourcePage(offset / PageSize + 1, new None());
			yield return page;
			offset += PageSize;
		}
	}

	private async Task<(MdblistTypes.ListItems.Root Page, bool HasMore)> FetchPageAsync(string identifier, int offset, CancellationToken cancellationToken)
	{
		var endpoint = string.Format(CultureInfo.InvariantCulture, MdblistEndpoints.Page, MdblistLists.ItemsPath(identifier), PageSize, offset);
		var apiKey = await apiKeys
			.GetAsync(ApiKeyProvider.Mdblist, cancellationToken)
			.GetOrThrow(_ => new InvalidOperationException("No MDBList API key is configured. Add one in Settings."));
		using var response = await client.SendAsync(HttpMethod.Get, endpoint, apiKey, cancellationToken);
		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			throw new FeedIdentifierNotFoundException(identifier);
		}
		response.EnsureSuccessStatusCode();
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var page = JsonSerializer.Deserialize<MdblistTypes.ListItems.Root>(stream)!;
		var hasMore = response.Headers.TryGetValues(HasMoreHeader, out var values) && values.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));
		return (page, hasMore);
	}

	private static Optional<IdentifyHints> BuildHints(FeedItemResolution pair)
	{
		return Optional
			.Of(ProviderValues.Text(pair.ListItem.title))
			.Map(title => new IdentifyHints
			{
				Title = title,
				Year = pair.ListItem.release_year,
				MediaType = pair.MediaType,
			});
	}

	private static UnmatchedMedia BuildUnmatched(MdblistTypes.ListItems.UnmatchedListItem item)
	{
		return new UnmatchedMedia(
			[.. MdblistIds.Of(item.ids, item.MediaType)],
			MediaDetails.Empty(item.MediaType) with
			{
				Title = ProviderValues.Text(item.title),
				ReleaseDate = ProviderValues.YearStart(item.release_year),
			}
		);
	}

	private readonly record struct FeedItemResolution
	{
		public MdblistTypes.ListItems.IListItem ListItem { get; private init; }
		public MediaType MediaType { get; private init; }

		public static FeedItemResolution Show(MdblistTypes.ListItems.IListItem item) => new() { ListItem = item, MediaType = MediaType.Show };

		public static FeedItemResolution Movie(MdblistTypes.ListItems.IListItem item) => new() { ListItem = item, MediaType = MediaType.Movie };
	}
}
