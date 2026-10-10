using API.Http;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;

namespace API.Mdblist;

internal sealed class MdblistEnricher(MdblistTitles titles) : IDetailsEnricher
{
	public MediaFields Fills { get; } = MediaFields.Rated(RemoteService.Mdblist.SuppliedRatingSources());

	public MediaFields Defers => MediaFields.None;

	public IReadOnlyList<MediaProvider> Accepts { get; } =
	[MediaProvider.Imdb, MediaProvider.Tmdb, MediaProvider.Tvdb, MediaProvider.Trakt, MediaProvider.Mal, MediaProvider.Mdblist];

	public Task<Answer<FoundMedia>> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		return LookUpResultAsync(id, cancellationToken).GuardedAsync(RemoteService.Mdblist);
	}

	private async Task<Result<FoundMedia>> LookUpResultAsync(IExternalMediaId id, CancellationToken cancellationToken)
	{
		return await titles
			.FindAsync(id, cancellationToken)
			.Map(title => title.Match(found => MdblistFound.Of(found, id.Type), () => new FoundMedia([id], MediaDetails.Empty(id.Type))));
	}
}
