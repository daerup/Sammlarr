using System.Globalization;
using System.Runtime.CompilerServices;
using Contracts.Datasets;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;

namespace API.Anime;

internal sealed class LaterSeasons(IAnimeIds map, IDatasetLoads loads)
{
	internal async IAsyncEnumerable<IdentifyRequest> ShowsOf(
		IAsyncEnumerable<IdentifyRequest> requests,
		[EnumeratorCancellation] CancellationToken cancellationToken
	)
	{
		var loaded = await loads.FindAsync(DatasetNames.AnimeIds, cancellationToken);
		_ = loaded.ToResult(AnimeListsIdConverter.NotLoaded).GetOrThrow(error => new InvalidOperationException(error.Reason));

		await foreach (var request in requests.WithCancellation(cancellationToken))
		{
			yield return await ShowOfAsync(request, cancellationToken);
		}
	}

	private async Task<IdentifyRequest> ShowOfAsync(IdentifyRequest request, CancellationToken cancellationToken)
	{
		var season = await request.Candidates.Where(IsAnimeId).FirstSomeAsync(id => map.FindAsync(id, cancellationToken));
		return await season.Map(entry => entry.SeriesTvdb).Match(series => ShowAsync(request, series, cancellationToken), () => Task.FromResult(request));
	}

	private async Task<IdentifyRequest> ShowAsync(IdentifyRequest request, int series, CancellationToken cancellationToken)
	{
		var seriesId = TvdbId.From(series.ToString(CultureInfo.InvariantCulture), MediaType.Show);
		var show = await map.FindAsync(seriesId, cancellationToken);
		var ids = show.Map(entry => AnimeListsIdConverter.Ids(entry, MediaType.Show)).GetValueOr(() => [seriesId]);
		return new IdentifyRequest(ids, new None()) { Description = request.Hints };
	}

	private static bool IsAnimeId(IExternalMediaId id) => id.Source is MediaProvider.AniList or MediaProvider.Mal;
}
