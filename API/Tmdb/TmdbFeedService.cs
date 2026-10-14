using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using API.Feeds;
using API.Http;
using API.Tmdb.Types;
using Contracts.Auth;
using Contracts.Feed;
using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Auth;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;
using Observability.Feed;
using Shared.Options;

namespace API.Tmdb;

internal sealed class TmdbFeedService(TmdbClient client, TmdbOptions options, IMediaService media, IApiKeyService apiKeys) : IFeedProvider
{
	private static readonly Error NotAList = Error.From(
		"Paste the list's link from themoviedb.org or its number, or a themoviedb.org movie, TV, discover, keyword, company or network page."
	);

	public Task<Result<string>> NormalizeAsync(string input, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(TmdbLists.Parse(input).Match(list => Success.Of(list), () => Page(input)));
	}

	private static Result<string> Page(string input)
	{
		return SiteLink.Parse(input, "themoviedb.org").ToResult(NotAList).Map(TmdbPages.Of).Map(page => page.Stored());
	}

	public IAsyncEnumerable<Listed<MediaMatchResult>> GetFeedItemsAsync(string identifier, FieldNeeds needs, CancellationToken cancellationToken = default)
	{
		return media.LoadListAsync(EntriesAsync(identifier, cancellationToken), needs, cancellationToken);
	}

	private async IAsyncEnumerable<ListTitle> EntriesAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var items = TmdbPages.IsPage(identifier) ? PageItemsAsync(identifier, cancellationToken).Take(FeedPages.Titles) : ItemsAsync(identifier, cancellationToken);
		await foreach (var item in items.WithCancellation(cancellationToken))
		{
			yield return Entry(item);
		}
	}

	private static ListTitle Entry(TmdbTypes.Lists.Item item)
	{
		return TypeOf(item)
			.Match<MediaType, ListTitle>(
				type => new IdentifyRequest([TmdbId.From(Number(item.Id), type)], Hints(item, type)) { Sources = [RemoteService.Tmdb] },
				() => Unmatched(item)
			);
	}

	private async IAsyncEnumerable<TmdbTypes.Lists.Item> PageItemsAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var page = SiteLink
			.Parse(identifier, "themoviedb.org")
			.ToResult(NotAList)
			.Map(TmdbPages.Of)
			.GetOrThrow(error => new InvalidOperationException(error.Reason));
		var type = TmdbPages.Kind(page);
		var apiKey = await KeyAsync(cancellationToken);
		var pages = PagesAsync(
			async number =>
			{
				using var response = await client.SendAsync(HttpMethod.Get, TmdbPages.Request(page, number), apiKey, cancellationToken);
				return await ReadAsync(response, () => new FeedIdentifierNotFoundException(identifier), cancellationToken);
			},
			_ => new None()
		);
		await foreach (var item in pages.WithCancellation(cancellationToken))
		{
			yield return item with
			{
				Type = type,
			};
		}
	}

	private async IAsyncEnumerable<TmdbTypes.Lists.Item> ItemsAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var token = (await apiKeys.GetAsync(ApiKeyProvider.TmdbReadToken, cancellationToken)).ToOptional();
		await foreach (
			var item in PagesAsync(page => PageAsync(identifier, page, token, cancellationToken), answer => Optional.Of(answer.TotalPages).When(total => total > 0))
				.WithCancellation(cancellationToken)
		)
		{
			yield return item;
		}
	}

	private static async IAsyncEnumerable<TmdbTypes.Lists.Item> PagesAsync(
		Func<int, Task<TmdbTypes.Lists.Page>> read,
		Func<TmdbTypes.Lists.Page, Optional<int>> total
	)
	{
		var pages = 1;
		for (var page = 1; page <= pages; page++)
		{
			var answer = await SourcePages.ReadAsync(page, () => read(page));
			pages = answer.TotalPages ?? 1;
			FeedRunSpans.SourcePage(page, total(answer));
			foreach (var item in answer.Entries)
			{
				yield return item;
			}
		}
	}

	private async Task<TmdbTypes.Lists.Page> PageAsync(string identifier, int page, Optional<IApiKey> token, CancellationToken cancellationToken)
	{
		using var response = await token.Match(
			found => client.SendWithTokenAsync(HttpMethod.Get, V4Page(identifier, page), found, cancellationToken),
			() => V3PageAsync(identifier, page, cancellationToken)
		);
		return await ReadAsync(
			response,
			() => token.Match(_ => (Exception)new FeedIdentifierNotFoundException(identifier), () => NotFoundWithTheKey(identifier)),
			cancellationToken
		);
	}

	private static async Task<TmdbTypes.Lists.Page> ReadAsync(HttpResponseMessage response, Func<Exception> notFound, CancellationToken cancellationToken)
	{
		if (response.StatusCode is HttpStatusCode.NotFound)
		{
			throw notFound();
		}

		response.EnsureSuccessStatusCode();
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		return await JsonSerializer.DeserializeAsync<TmdbTypes.Lists.Page>(stream, cancellationToken: cancellationToken)
			?? throw new InvalidDataException("TMDB answered an empty page.");
	}

	private async Task<IApiKey> KeyAsync(CancellationToken cancellationToken)
	{
		return await apiKeys
			.GetAsync(ApiKeyProvider.Tmdb, cancellationToken)
			.GetOrThrow(_ => new InvalidOperationException("No TMDB API key is configured. Add one in Settings."));
	}

	private async Task<HttpResponseMessage> V3PageAsync(string identifier, int page, CancellationToken cancellationToken)
	{
		var apiKey = await KeyAsync(cancellationToken);
		return await client.SendAsync(HttpMethod.Get, string.Format(CultureInfo.InvariantCulture, TmdbEndpoints.List, identifier, page), apiKey, cancellationToken);
	}

	private string V4Page(string identifier, int page)
	{
		return $"{options.V4BaseUrl.TrimEnd('/')}/{string.Format(CultureInfo.InvariantCulture, TmdbEndpoints.List, identifier, page)}";
	}

	private static InvalidOperationException NotFoundWithTheKey(string identifier)
	{
		return new InvalidOperationException(
			$"TMDB has no list {identifier} for the API key. A list only TMDB's v4 API serves needs the read access token under TMDB's key in Settings › Providers."
		);
	}

	private static Optional<MediaType> TypeOf(TmdbTypes.Lists.Item item)
	{
		return item.Type switch
		{
			"movie" => Some.Of(MediaType.Movie),
			"tv" => Some.Of(MediaType.Show),
			_ => new None(),
		};
	}

	private static Optional<IdentifyHints> Hints(TmdbTypes.Lists.Item item, MediaType type)
	{
		return Optional
			.Of(item.DisplayTitle)
			.Map(title => new IdentifyHints
			{
				Title = title,
				Year = item.Released(type)?.Year ?? 0,
				MediaType = type,
			});
	}

	private static UnmatchedMedia Unmatched(TmdbTypes.Lists.Item item)
	{
		return new UnmatchedMedia([TmdbId.From(Number(item.Id), MediaType.Unknown)], MediaDetails.Empty(MediaType.Unknown) with { Title = item.DisplayTitle });
	}

	private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
