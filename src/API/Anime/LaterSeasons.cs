using System.Globalization;
using System.Runtime.CompilerServices;
using Contracts.Datasets;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;

namespace API.Anime;

internal sealed class LaterSeasons(AnimeIdsInRun map, IDatasetLoads loads, IAnimePrequels prequels)
{
	private const int PageSize = 50;

	internal async IAsyncEnumerable<ListTitle> ShowsOf(IAsyncEnumerable<IdentifyRequest> requests, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var loaded = await loads.FindAsync(DatasetNames.AnimeIds, cancellationToken);
		_ = loaded.ToResult(AnimeListsIdConverter.NotLoaded).GetOrThrow(error => new InvalidOperationException(error.Reason));

		await foreach (var burst in requests.BurstsAsync(cancellationToken))
		{
			foreach (var page in burst.Chunk(PageSize))
			{
				foreach (var show in await ShowsOfAsync(page, cancellationToken))
				{
					yield return show;
				}
			}
		}
	}

	private async Task<IReadOnlyList<ListTitle>> ShowsOfAsync(IReadOnlyList<IdentifyRequest> page, CancellationToken cancellationToken)
	{
		await map.PrimeIdsAsync([.. page.SelectMany(request => request.Candidates.Where(IsAnimeId))], cancellationToken);
		List<Optional<AnimeIdMapping>> seasons = [];
		foreach (var request in page)
		{
			seasons.Add(await SeasonOfAsync(request, cancellationToken));
		}

		var throughPrequels = await SeriesThroughPrequelsAsync(seasons, cancellationToken);
		List<Result<Optional<Series>>> series = [.. seasons.Select(season => SeriesOf(season, throughPrequels))];

		await map.PrimeIdsAsync([.. series.Select(found => found.GetValueOr(new None())).WhereSome().Select(found => SeriesId(found.Tvdb))], cancellationToken);
		List<ListTitle> shows = [];
		foreach (var (request, show) in page.Zip(series))
		{
			shows.Add(await show.Match(found => TitleAsync(request, found, cancellationToken), _ => CouldNotLookUp(request)));
		}

		return shows;
	}

	private async Task<Optional<AnimeIdMapping>> SeasonOfAsync(IdentifyRequest request, CancellationToken cancellationToken)
	{
		return await request.Candidates.Where(IsAnimeId).FirstSomeAsync(id => EntryAsync(id, cancellationToken));
	}

	private static Result<Optional<Series>> SeriesOf(Optional<AnimeIdMapping> season, IReadOnlyDictionary<int, Result<Optional<int>>> throughPrequels)
	{
		var direct = season.Map(entry => entry.SeriesTvdb).Map(tvdb => new Series(tvdb, false));
		var prequel = season.Map(entry => entry.Mal).Map(throughPrequels.GetValueOrNone).GetValueOr(Success.Of<Optional<int>>(new None()));
		return prequel.Map(found => direct.Or(() => found.Map(tvdb => new Series(tvdb, true))));
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

	private async Task<ListTitle> TitleAsync(IdentifyRequest request, Optional<Series> series, CancellationToken cancellationToken)
	{
		return await series.Match(found => ShowAsync(request, found, cancellationToken), () => request);
	}

	private async Task<IdentifyRequest> ShowAsync(IdentifyRequest request, Series series, CancellationToken cancellationToken)
	{
		var seriesId = SeriesId(series.Tvdb);
		var show = await EntryAsync(seriesId, cancellationToken);
		var ids = show.Map(entry => AnimeListsIdConverter.Ids(entry, MediaType.Show)).GetValueOr(() => [seriesId]);
		ValueSet<RemoteService> sources = series.ThroughPrequel ? [RemoteService.AnimeLists, RemoteService.AniList] : [RemoteService.AnimeLists];
		return new IdentifyRequest(ids, new None()) { Description = request.Hints, Sources = sources };
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

	private static bool IsAnimeId(IExternalMediaId id) => id.Source is MediaProvider.AniList or MediaProvider.Mal;

	private readonly record struct Series(int Tvdb, bool ThroughPrequel);
}
