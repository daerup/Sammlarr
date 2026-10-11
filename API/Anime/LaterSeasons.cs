using System.Globalization;
using System.Runtime.CompilerServices;
using Contracts.Datasets;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;

namespace API.Anime;

internal sealed class LaterSeasons(AnimeIdsInRun map, IDatasetLoads loads)
{
	private const int PageSize = 50;

	internal async IAsyncEnumerable<IdentifyRequest> ShowsOf(
		IAsyncEnumerable<IdentifyRequest> requests,
		[EnumeratorCancellation] CancellationToken cancellationToken
	)
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

	private async Task<IReadOnlyList<IdentifyRequest>> ShowsOfAsync(IReadOnlyList<IdentifyRequest> page, CancellationToken cancellationToken)
	{
		await map.PrimeIdsAsync([.. page.SelectMany(request => request.Candidates.Where(IsAnimeId))], cancellationToken);
		List<Optional<int>> series = [];
		foreach (var request in page)
		{
			series.Add(await SeriesOfAsync(request, cancellationToken));
		}

		await map.PrimeIdsAsync([.. series.WhereSome().Select(SeriesId)], cancellationToken);
		List<IdentifyRequest> shows = [];
		foreach (var (request, show) in page.Zip(series))
		{
			shows.Add(await show.Match(found => ShowAsync(request, found, cancellationToken), () => request));
		}

		return shows;
	}

	private async Task<Optional<int>> SeriesOfAsync(IdentifyRequest request, CancellationToken cancellationToken)
	{
		var season = await request.Candidates.Where(IsAnimeId).FirstSomeAsync(id => EntryAsync(id, cancellationToken));
		return season.Map(entry => entry.SeriesTvdb);
	}

	private async Task<IdentifyRequest> ShowAsync(IdentifyRequest request, int series, CancellationToken cancellationToken)
	{
		var seriesId = SeriesId(series);
		var show = await EntryAsync(seriesId, cancellationToken);
		var ids = show.Map(entry => AnimeListsIdConverter.Ids(entry, MediaType.Show)).GetValueOr(() => [seriesId]);
		return new IdentifyRequest(ids, new None()) { Description = request.Hints, Sources = [RemoteService.AnimeLists] };
	}

	private async Task<Optional<AnimeIdMapping>> EntryAsync(IExternalMediaId id, CancellationToken cancellationToken)
	{
		var entry = await map.FindAsync(id, cancellationToken);
		return entry.GetValueOr(new None());
	}

	private static IExternalMediaId SeriesId(int series) => TvdbId.From(series.ToString(CultureInfo.InvariantCulture), MediaType.Show);

	private static bool IsAnimeId(IExternalMediaId id) => id.Source is MediaProvider.AniList or MediaProvider.Mal;
}
