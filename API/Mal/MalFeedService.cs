using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using API.Anime;
using API.Feeds;
using API.Http;
using API.Mal.Types;
using Contracts;
using Contracts.Auth;
using Contracts.Feed;
using Contracts.Media;
using Domain.Auth;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;
using Observability.Feed;

namespace API.Mal;

internal sealed partial class MalFeedService(MalClient client, IMediaService media, IApiKeyService apiKeys, LaterSeasons seasons, IDateTimeProvider clock)
	: IFeedProvider
{
	private const string Fields = "list_status,media_type,start_date,alternative_titles";

	private static readonly Error NotAList = Error.From(
		"Paste a MyAnimeList username, the list's myanimelist.net/animelist link, or a myanimelist.net top anime or season link."
	);

	public Task<Result<string>> NormalizeAsync(string input, CancellationToken cancellationToken = default)
	{
		var match = ListPattern().Match(input.Trim());
		var list = Optional.When(match.Success, () => match.Groups["user"].Value);
		return Task.FromResult(list.Match(user => Success.Of(user), () => Page(input)));
	}

	private static Result<string> Page(string input)
	{
		return SiteLink.Parse(input, "myanimelist.net").ToResult(NotAList).Map(MalPages.Of).Map(page => page.Stored());
	}

	public IAsyncEnumerable<Listed<MediaMatchResult>> GetFeedItemsAsync(string identifier, FieldNeeds needs, CancellationToken cancellationToken = default)
	{
		return media.LoadListAsync(seasons.ShowsOf(RequestsAsync(identifier, cancellationToken), cancellationToken), needs, cancellationToken);
	}

	private async IAsyncEnumerable<IdentifyRequest> RequestsAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var clientId = await apiKeys
			.GetAsync(ApiKeyProvider.Mal, cancellationToken)
			.GetOrThrow(_ => new InvalidOperationException("No MyAnimeList client ID is configured. Add one in Settings."));
		var isPage = MalPages.IsPage(identifier);
		var read = AnimeAsync(identifier, First(identifier, isPage), isPage, clientId, cancellationToken).Where(MalAnime.IsTitle);
		await foreach (var anime in (isPage ? read.Take(FeedPages.Titles) : read).WithCancellation(cancellationToken))
		{
			var type = MalAnime.TypeOf(anime);
			yield return new IdentifyRequest([MalAnime.Id(anime, type)], MalAnime.Hints(anime, type)) { Sources = [RemoteService.Mal] };
		}
	}

	private string First(string identifier, bool isPage)
	{
		return isPage
			? MalPages.Request(
				SiteLink.Parse(identifier, "myanimelist.net").ToResult(NotAList).Map(MalPages.Of).GetOrThrow(error => new InvalidOperationException(error.Reason)),
				DateOnly.FromDateTime(clock.Now.UtcDateTime)
			)
			: string.Format(CultureInfo.InvariantCulture, "users/{0}/animelist?fields={1}&limit=1000", Uri.EscapeDataString(identifier), Fields);
	}

	private async IAsyncEnumerable<MalTypes.Anime> AnimeAsync(
		string identifier,
		string first,
		bool isPage,
		IApiKey clientId,
		[EnumeratorCancellation] CancellationToken cancellationToken
	)
	{
		var page = first;
		var number = 0;
		while (page.Length > 0)
		{
			var answer = await SourcePages.ReadAsync(number + 1, () => PageAsync(identifier, page, isPage, clientId, cancellationToken));
			var data = answer.Data ?? throw new InvalidDataException("MyAnimeList answered a page without data.");
			number++;
			FeedRunSpans.SourcePage(number, new None());
			foreach (var anime in data.Select(item => item.Node).OfType<MalTypes.Anime>())
			{
				yield return anime;
			}

			page = ProviderValues.Text(answer.Paging?.Next) ?? string.Empty;
		}
	}

	private async Task<MalTypes.ListPage> PageAsync(string identifier, string page, bool isPage, IApiKey clientId, CancellationToken cancellationToken)
	{
		using var response = await client.GetAsync(page, clientId, cancellationToken);
		if (response.StatusCode is HttpStatusCode.NotFound)
		{
			throw new FeedIdentifierNotFoundException(identifier);
		}

		if (response.StatusCode is HttpStatusCode.Forbidden)
		{
			throw new InvalidOperationException(
				isPage ? "MyAnimeList refused the page (403)." : $"MyAnimeList keeps the list of {identifier} private, so Sammlarr cannot read it."
			);
		}

		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<MalTypes.ListPage>(cancellationToken)
			?? throw new InvalidDataException("MyAnimeList answered an empty list page.");
	}

	[GeneratedRegex(@"^(?:(?:https?://)?(?:www\.)?myanimelist\.net/(?:animelist|profile)/)?(?<user>[A-Za-z0-9_-]{2,16})/?(?:[?#].*)?$")]
	private static partial Regex ListPattern();
}
