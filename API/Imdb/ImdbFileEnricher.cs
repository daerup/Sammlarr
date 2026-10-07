using Contracts.Datasets;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;

namespace API.Imdb;

internal sealed class ImdbFileEnricher(IImdbRatings ratings, IDatasetLoads loads) : IDetailsEnricher
{
	public MediaFields Fills { get; } = MediaFields.Rated(RemoteService.ImdbDatasets.SuppliedRatingSources());

	public MediaFields Defers => MediaFields.None;

	public IReadOnlyList<MediaProvider> Accepts { get; } = [MediaProvider.Imdb];

	public async Task<Result<FoundMedia>> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		var loaded = await loads.FindAsync(DatasetNames.ImdbRatings, cancellationToken);
		return await loaded.ToResult("IMDb's ratings file has not been loaded yet.").Map(_ => FindAsync(id, cancellationToken));
	}

	private async Task<FoundMedia> FindAsync(IExternalMediaId id, CancellationToken cancellationToken)
	{
		var rating = await ratings.FindAsync(id.Value, cancellationToken);
		return new FoundMedia([id], MediaDetails.Empty(id.Type) with { Ratings = rating.Map(found => MediaRatings.From([found])).GetValueOr(MediaRatings.None) });
	}
}
