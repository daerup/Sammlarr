using Contracts.Media;
using Domain.Media;
using Domain.Providers;

namespace API.Tvmaze;

internal sealed class TvmazeEnricher(TvmazeApi tvmaze) : IDetailsEnricher
{
	public MediaFields Fills { get; } =
		MediaField.Title | MediaField.Summary | MediaField.Poster | MediaField.ReleaseDate | MediaField.Genres | MediaField.RuntimePerEpisode;

	public MediaFields Defers { get; } = MediaField.Title;

	public IReadOnlyList<MediaProvider> Accepts { get; } = [MediaProvider.Tvmaze, MediaProvider.Imdb, MediaProvider.Tvdb];

	public Task<Result<FoundMedia>> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		return tvmaze
			.LookUpAsync(id, cancellationToken)
			.Map(show =>
				show.Match(found => new FoundMedia([.. TvmazeFound.Ids(found)], TvmazeFound.Details(found)), () => new FoundMedia([id], MediaDetails.Empty(id.Type)))
			);
	}
}
