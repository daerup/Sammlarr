using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using API.AniList.Types;
using API.Anime;
using API.Feeds;
using API.Http;
using Contracts;
using Contracts.Feed;
using Contracts.Media;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;
using Observability.Feed;

namespace API.AniList;

internal sealed partial class AniListFeedService(AniListClient client, IMediaService media, LaterSeasons seasons, AniListTitles titles, IDateTimeProvider clock)
	: IFeedProvider
{
	private const string Query =
		$"query ($user: String, $status: MediaListStatus) {{ MediaListCollection(userName: $user, type: ANIME, status: $status) {{ lists {{ entries {{ media {{ {AniListMedia.Fields} }} }} }} }} }}";

	private const string SearchQuery =
		"query ($page: Int = 1, $type: MediaType, $isAdult: Boolean = false, $search: String, $format: [MediaFormat], $status: MediaStatus, $countryOfOrigin: CountryCode, $source: MediaSource, $season: MediaSeason, $seasonYear: Int, $year: String, $yearLesser: FuzzyDateInt, $yearGreater: FuzzyDateInt, $genres: [String], $excludedGenres: [String], $tags: [String], $excludedTags: [String], $minimumTagRank: Int, $sort: [MediaSort] = [POPULARITY_DESC, SCORE_DESC]) "
		+ $"{{ Page(page: $page, perPage: 50) {{ pageInfo {{ hasNextPage }} media(type: $type, season: $season, format_in: $format, status: $status, countryOfOrigin: $countryOfOrigin, source: $source, search: $search, seasonYear: $seasonYear, startDate_like: $year, startDate_lesser: $yearLesser, startDate_greater: $yearGreater, genre_in: $genres, genre_not_in: $excludedGenres, tag_in: $tags, tag_not_in: $excludedTags, minimumTagRank: $minimumTagRank, sort: $sort, isAdult: $isAdult) {{ {AniListMedia.Fields} }} }} }}";

	private const string GenresQuery = "query { GenreCollection }";

	private static readonly string[] Statuses = ["current", "planning", "completed", "dropped", "paused", "repeating"];

	private static readonly Error NotAList = Error.From(
		"Paste an AniList username, username/status (current, planning, completed, dropped, paused or repeating), the list's anilist.co link, or an anilist.co/search/anime link."
	);

	public Task<Result<string>> NormalizeAsync(string input, CancellationToken cancellationToken = default)
	{
		var userShaped = ListPattern.IsMatch(input.Trim());
		return Task.FromResult(UserList(input).Match(list => Success.Of(list), () => Page(input).MapError(error => userShaped ? NotAList : error)));
	}

	public IAsyncEnumerable<Listed<MediaMatchResult>> GetFeedItemsAsync(string identifier, FieldNeeds needs, CancellationToken cancellationToken = default)
	{
		return media.LoadListAsync(seasons.ShowsOf(RequestsAsync(identifier, cancellationToken), cancellationToken), needs, cancellationToken);
	}

	private static Optional<string> UserList(string input)
	{
		var match = ListPattern.Match(input.Trim());
		var status = match.Groups["status"].Value.ToLowerInvariant();
		return Optional.When(
			match.Success && (status.Length == 0 || Statuses.Contains(status)),
			() => status.Length == 0 ? match.Groups["user"].Value : $"{match.Groups["user"].Value}/{status}"
		);
	}

	private static Result<string> Page(string input)
	{
		return SiteLink.Parse(input, "anilist.co").ToResult(NotAList).Map(AniListPages.Of).Map(page => page.Stored());
	}

	private async IAsyncEnumerable<IdentifyRequest> RequestsAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var read = identifier.StartsWith(AniListPages.Search, StringComparison.Ordinal)
			? SearchAsync(identifier, cancellationToken).Take(FeedPages.Titles)
			: ListAsync(identifier, cancellationToken).Where(AniListMedia.IsTitle);
		await foreach (var found in read.WithCancellation(cancellationToken))
		{
			titles.Remember(found);
			var type = AniListMedia.TypeOf(found);
			yield return new IdentifyRequest([.. AniListMedia.Ids(found, type)], AniListMedia.Hints(found, type)) { Sources = [RemoteService.AniList] };
		}
	}

	private async IAsyncEnumerable<AniListTypes.Media> ListAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var parts = identifier.Split('/');
		var variables = new JsonObject { ["user"] = parts[0] };
		parts.Skip(1).FirstOrNone().Apply(status => variables["status"] = status.ToUpperInvariant());
		var answer = await SourcePages.ReadAsync(1, () => client.AskAsync<AniListTypes.ListData>(Query, variables, cancellationToken));
		var collection = answer.GetOrThrow(error => Failure(identifier, error));
		FeedRunSpans.SourcePage(1, Some.Of(1));
		foreach (var found in (collection.Collection?.Lists ?? []).SelectMany(list => list.Entries ?? []).Select(entry => entry.Media).OfType<AniListTypes.Media>())
		{
			yield return found;
		}
	}

	private async IAsyncEnumerable<AniListTypes.Media> SearchAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var search = SiteLink
			.Parse(identifier, "anilist.co")
			.ToResult(NotAList)
			.Map(AniListPages.Of)
			.GetOrThrow(error => new InvalidOperationException(error.Reason));
		var genres = AniListPages.NeedsGenres(search) ? await GenresAsync(cancellationToken) : new HashSet<string>(StringComparer.Ordinal);
		var music = AniListPages.AsksForMusic(search);
		var variables = AniListPages
			.Variables(search, genres, DateOnly.FromDateTime(clock.Now.UtcDateTime))
			.GetOrThrow(error => new InvalidOperationException(error.Reason));
		var page = 1;
		var more = true;
		while (more)
		{
			var asked = variables.DeepClone().AsObject();
			asked["page"] = page;
			var answer = await SourcePages.ReadAsync(page, () => client.AskAsync<AniListTypes.PageData>(SearchQuery, asked, cancellationToken));
			var found = answer.GetOrThrow(Refused).Page ?? throw new InvalidDataException("AniList answered a search without a page.");
			FeedRunSpans.SourcePage(page, new None());
			foreach (var title in (found.Items ?? []).Where(title => music || AniListMedia.IsTitle(title)))
			{
				yield return title;
			}

			more = found.PageInfo?.HasNextPage ?? false;
			page++;
		}
	}

	private async Task<IReadOnlySet<string>> GenresAsync(CancellationToken cancellationToken)
	{
		var answer = await client.AskAsync<AniListTypes.GenreData>(GenresQuery, new JsonObject(), cancellationToken);
		return answer.GetOrThrow(Refused).Genres?.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
	}

	private static Exception Refused(Error error)
	{
		return error
			.CausedBy<AniListAnswerException>()
			.Match<AniListAnswerException, Exception>(
				problem => new InvalidOperationException($"AniList refused the search: {problem.Message}"),
				() => error.Exception.GetValueOr(() => new InvalidOperationException(error.Reason))
			);
	}

	private static Exception Failure(string identifier, Error error)
	{
		return error
			.CausedBy<AniListAnswerException>()
			.Match(
				problem =>
					problem.Message.Contains("Private", StringComparison.OrdinalIgnoreCase)
						? new InvalidOperationException($"AniList keeps the list of {identifier.Split('/')[0]} private, so Sammlarr cannot read it.")
						: new FeedIdentifierNotFoundException(identifier),
				() => error.Exception.GetValueOr(() => new InvalidOperationException(error.Reason))
			);
	}

	[GeneratedRegex(
		@"^(?:(?:https?://)?(?:www\.)?anilist\.co/user/)?(?<user>[A-Za-z0-9_-]+)(?:/animelist)?(?:/(?<status>[A-Za-z]+))?/?$",
		RegexOptions.None,
		matchTimeoutMilliseconds: 1000
	)]
	private static partial Regex ListPattern { get; }
}
