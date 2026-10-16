using API.Http;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;

namespace API.Imdb;

internal sealed class ImdbFileEnricher(ImdbRatingsInRun ratings) : IDetailsEnricher
{
	private static readonly Error NotLoaded = Error.From(
		"IMDb's ratings file has not been loaded yet.",
		new RemoteUnavailableException(RemoteService.ImdbDatasets, sent: false)
	);

	public MediaFields Fills { get; } = MediaFields.Rated(RemoteService.ImdbDatasets.SuppliedRatingSources());

	public MediaFields Defers => MediaFields.None;

	public IReadOnlyList<MediaProvider> Accepts { get; } = [MediaProvider.Imdb];

	public Task<Answer<FoundMedia>> LookUpAsync(IExternalMediaId id, ValueSet<IIdentityKey> keys, CancellationToken cancellationToken = default)
	{
		return LookUpResultAsync(id, cancellationToken).GuardedAsync(RemoteService.ImdbDatasets);
	}

	private async Task<Result<FoundMedia>> LookUpResultAsync(IExternalMediaId id, CancellationToken cancellationToken)
	{
		var rating = await ratings.FindAsync(id.Value, cancellationToken);
		return rating.ToResult(NotLoaded).Map(found => Found(id, found));
	}

	private static FoundMedia Found(IExternalMediaId id, Optional<ImdbRating> rating)
	{
		return new FoundMedia([id], MediaDetails.Empty(id.Type) with { Ratings = rating.Map(found => MediaRatings.From([found])).GetValueOr(MediaRatings.None) });
	}
}
