using API.Http;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;

namespace API.Trakt;

internal sealed class TraktEnricher(TraktTitles titles) : IDetailsEnricher
{
	public MediaFields Fills { get; } =
		MediaField.Summary
		| MediaField.ReleaseDate
		| MediaField.Genres
		| MediaField.Runtime
		| MediaField.RuntimePerEpisode
		| MediaFields.Rated(RemoteService.Trakt.SuppliedRatingSources());

	public MediaFields Defers { get; } = MediaFields.None;

	public IReadOnlyList<MediaProvider> Accepts { get; } = [MediaProvider.Trakt, MediaProvider.Imdb];

	public Task<Answer<FoundMedia>> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		return LookUpResultAsync(id, cancellationToken).GuardedAsync(RemoteService.Trakt);
	}

	private async Task<Result<FoundMedia>> LookUpResultAsync(IExternalMediaId id, CancellationToken cancellationToken)
	{
		return await titles
			.FindAsync(id, cancellationToken)
			.Map(title =>
				title.Match(
					found => new FoundMedia([.. TraktTitle.Ids(found, id.Type)], TraktTitle.Details(found, id.Type)),
					() => new FoundMedia([id], MediaDetails.Empty(id.Type))
				)
			);
	}
}
