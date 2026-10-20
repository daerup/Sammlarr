using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using Contracts.Datasets;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;

namespace API.Anime;

internal sealed class LaterSeasons(AnimeIdsInRun map, IDatasetLoads loads, IAnimePrequels prequels, IShowParts parts, IMediaService media)
{
	private const int PageSize = 50;

	internal async IAsyncEnumerable<Listed<MediaMatchResult>> LoadAsync(
		IAsyncEnumerable<IdentifyRequest> requests,
		FieldNeeds needs,
		[EnumeratorCancellation] CancellationToken cancellationToken
	)
	{
		var noted = new ConcurrentDictionary<int, Part>();
		var titles = WalkAsync(requests, cancellationToken).Select((walked, position) => Noted(walked, position, noted));
		await foreach (var listed in media.LoadListAsync(titles, needs, cancellationToken))
		{
			if (noted.TryRemove(listed.Position, out var part) && listed.Item is MatchedMedia matched)
			{
				await parts.AddAsync(part.Ids, Some.Of(matched.Identity.Id), part.Place, cancellationToken);
			}

			yield return listed;
		}
	}

	private static ListTitle Noted(Walked walked, int position, ConcurrentDictionary<int, Part> noted)
	{
		walked.Part.Apply(part => noted[position] = part);
		return walked.Title;
	}

	private async IAsyncEnumerable<Walked> WalkAsync(IAsyncEnumerable<IdentifyRequest> requests, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var loaded = await loads.FindAsync(DatasetNames.AnimeIds, cancellationToken);
		_ = loaded.ToResult(AnimeListsIdConverter.NotLoaded).GetOrThrow(error => new InvalidOperationException(error.Reason));

		await foreach (var burst in requests.BurstsAsync(cancellationToken))
		{
			foreach (var page in burst.Chunk(PageSize))
			{
				foreach (var walked in await ShowsOfAsync(page, cancellationToken))
				{
					yield return walked;
				}
			}
		}
	}

	private async Task<IReadOnlyList<Walked>> ShowsOfAsync(IReadOnlyList<IdentifyRequest> page, CancellationToken cancellationToken)
	{
		var stored = await parts.GetShowsAsync(AnimeIds(page), cancellationToken);
		List<Optional<IdentifyRequest>> answered = [.. page.Select(request => Answered(request, stored))];
		var walked = new Queue<Walked>(await UnansweredAsync([.. page.Where((_, index) => answered[index].IsNone())], cancellationToken));
		return [.. answered.Select(answer => answer.Match(request => new Walked(request, new None()), walked.Dequeue))];
	}

	private static Optional<IdentifyRequest> Answered(
		IdentifyRequest request,
		IReadOnlyDictionary<IExternalMediaId, Optional<IReadOnlyList<IExternalMediaId>>> shows
	)
	{
		return request
			.Candidates.Where(IsAnimeId)
			.Select(shows.GetValueOrNone)
			.WhereSome()
			.OrderBy(answer => answer.IsNone())
			.FirstOrNone()
			.Map(answer =>
				answer.Map(keys => new IdentifyRequest(keys, new None()) { Description = request.Hints, Sources = [RemoteService.AnimeLists] }).GetValueOr(request)
			);
	}

	private async Task<IReadOnlyList<Walked>> UnansweredAsync(IReadOnlyList<IdentifyRequest> page, CancellationToken cancellationToken)
	{
		await map.PrimeIdsAsync(AnimeIds(page), cancellationToken);
		List<Optional<AnimeIdMapping>> seasons = [];
		foreach (var request in page)
		{
			seasons.Add(await SeasonOfAsync(request, cancellationToken));
		}

		var throughPrequels = await SeriesThroughPrequelsAsync(seasons, cancellationToken);
		await StoreNoShowsAsync(seasons, throughPrequels, cancellationToken);
		List<Result<Optional<Series>>> series = [.. seasons.Select(season => SeriesOf(season, throughPrequels))];

		await map.PrimeIdsAsync([.. series.Select(found => found.GetValueOr(new None())).WhereSome().Select(found => SeriesId(found.Tvdb))], cancellationToken);
		List<Walked> shows = [];
		foreach (var (request, show) in page.Zip(series))
		{
			shows.Add(await show.Match(found => TitleAsync(request, found, cancellationToken), _ => new Walked(CouldNotLookUp(request), new None())));
		}

		return shows;
	}

	private async Task<Optional<AnimeIdMapping>> SeasonOfAsync(IdentifyRequest request, CancellationToken cancellationToken)
	{
		return await request.Candidates.Where(IsAnimeId).FirstSomeAsync(id => EntryAsync(id, cancellationToken));
	}

	private static Result<Optional<Series>> SeriesOf(Optional<AnimeIdMapping> season, IReadOnlyDictionary<int, Result<Optional<int>>> throughPrequels)
	{
		return season.Match(entry => SeriesOf(entry, throughPrequels), () => Success.Of<Optional<Series>>(new None()));
	}

	private static Result<Optional<Series>> SeriesOf(AnimeIdMapping season, IReadOnlyDictionary<int, Result<Optional<int>>> throughPrequels)
	{
		var direct = season.SeriesTvdb.Map(tvdb => new Series(tvdb, false, season));
		var prequel = season.Mal.Map(throughPrequels.GetValueOrNone).GetValueOr(Success.Of<Optional<int>>(new None()));
		return prequel.Map(found => direct.Or(() => found.Map(tvdb => new Series(tvdb, true, season))));
	}

	private async Task<IReadOnlyDictionary<int, Result<Optional<int>>>> SeriesThroughPrequelsAsync(
		IReadOnlyList<Optional<AnimeIdMapping>> seasons,
		CancellationToken cancellationToken
	)
	{
		int[] bare = [.. seasons.WhereSome().Where(HoldsNoIds).Select(entry => entry.Mal).WhereSome().Distinct()];
		if (bare.Length == 0)
		{
			return new Dictionary<int, Result<Optional<int>>>();
		}

		var asked = await prequels.PrequelsOfAsync(bare, cancellationToken);
		await map.PrimeIdsAsync(
			[.. asked.Values.Select(answer => answer.GetValueOr(new None())).WhereSome().Select(prequel => (IExternalMediaId)PrequelId(prequel))],
			cancellationToken
		);
		Dictionary<int, Result<Optional<int>>> series = [];
		foreach (var (season, answer) in asked)
		{
			series[season] = await answer.Map(prequel => ShowOfAsync(prequel, cancellationToken));
		}

		return series;
	}

	private async Task StoreNoShowsAsync(
		IReadOnlyList<Optional<AnimeIdMapping>> seasons,
		IReadOnlyDictionary<int, Result<Optional<int>>> throughPrequels,
		CancellationToken cancellationToken
	)
	{
		IExternalMediaId[] nowhere = [.. seasons.WhereSome().Where(season => LeadsNowhere(season, throughPrequels)).SelectMany(season => PartOf(season).Ids)];
		if (nowhere.Length > 0)
		{
			await parts.AddAsync(nowhere, new None(), new ShowPartPlace(new None(), new None()), cancellationToken);
		}
	}

	private static bool LeadsNowhere(AnimeIdMapping season, IReadOnlyDictionary<int, Result<Optional<int>>> throughPrequels)
	{
		return season.Mal.Map(throughPrequels.GetValueOrNone).Map(answer => answer.Match(tvdb => tvdb.IsNone(), _ => false)).GetValueOr(false);
	}

	private async Task<Optional<int>> ShowOfAsync(Optional<int> prequel, CancellationToken cancellationToken)
	{
		var entry = await prequel.Match(found => EntryAsync(PrequelId(found), cancellationToken), () => new None());
		return entry.Map(show => show.SeriesTvdb.Or(show.Tvdb));
	}

	private static bool HoldsNoIds(AnimeIdMapping entry)
	{
		return entry.Type is MediaType.Show && entry.Tvdb.IsNone() && entry.SeriesTvdb.IsNone() && entry.Tmdb.IsNone() && entry.Imdb.IsNone();
	}

	private static MalId PrequelId(int prequel) => MalId.From(prequel.ToString(CultureInfo.InvariantCulture), MediaType.Show);

	private async Task<Walked> TitleAsync(IdentifyRequest request, Optional<Series> series, CancellationToken cancellationToken)
	{
		return await series.Match(found => ShowAsync(request, found, cancellationToken), () => new Walked(request, new None()));
	}

	private async Task<Walked> ShowAsync(IdentifyRequest request, Series series, CancellationToken cancellationToken)
	{
		var seriesId = SeriesId(series.Tvdb);
		var show = await EntryAsync(seriesId, cancellationToken);
		var ids = show.Map(entry => AnimeListsIdConverter.Ids(entry, MediaType.Show)).GetValueOr(() => [seriesId]);
		ValueSet<RemoteService> sources = series.ThroughPrequel ? [RemoteService.AnimeLists, RemoteService.AniList] : [RemoteService.AnimeLists];
		return new Walked(new IdentifyRequest(ids, new None()) { Description = request.Hints, Sources = sources }, Some.Of(PartOf(series.Season)));
	}

	private static Part PartOf(AnimeIdMapping season)
	{
		return new Part([.. AnimeListsIdConverter.Ids(season, MediaType.Show).Where(IsAnimeId)], new ShowPartPlace(season.TvdbSeason, season.EpisodeOffset));
	}

	private static UnmatchedMedia CouldNotLookUp(IdentifyRequest request)
	{
		var details = request
			.Hints.Map(hints => MediaDetails.Empty(hints.MediaType) with { Title = hints.Title, ReleaseDate = ProviderValues.YearStart(hints.Year) })
			.GetValueOr(() => MediaDetails.Empty(request.Type));
		return new UnmatchedMedia([.. request.Candidates], details, UnmatchedReason.CouldNotLookUp);
	}

	private async Task<Optional<AnimeIdMapping>> EntryAsync(IExternalMediaId id, CancellationToken cancellationToken)
	{
		var entry = await map.FindAsync(id, cancellationToken);
		return entry.GetValueOr(new None());
	}

	private static IExternalMediaId SeriesId(int series) => TvdbId.From(series.ToString(CultureInfo.InvariantCulture), MediaType.Show);

	private static IExternalMediaId[] AnimeIds(IEnumerable<IdentifyRequest> page) => [.. page.SelectMany(request => request.Candidates.Where(IsAnimeId))];

	private static bool IsAnimeId(IExternalMediaId id) => id.Source is MediaProvider.AniList or MediaProvider.Mal;

	private readonly record struct Series(int Tvdb, bool ThroughPrequel, AnimeIdMapping Season);

	private sealed record Walked(ListTitle Title, Optional<Part> Part);

	private sealed record Part(IReadOnlyList<IExternalMediaId> Ids, ShowPartPlace Place);
}
