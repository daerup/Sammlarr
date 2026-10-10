using System.Globalization;
using API.Http;
using API.Tmdb.Types;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;

namespace API.Tmdb;

internal class TmdbDetailsEnricher(TmdbApi tmdbApi, TmdbArtwork artwork) : IDetailsEnricher
{
	public MediaFields Fills { get; } =
		MediaField.Title
		| MediaField.Tagline
		| MediaField.Summary
		| MediaField.Poster
		| MediaField.Backdrop
		| MediaField.ReleaseDate
		| MediaField.Genres
		| MediaField.Runtime
		| MediaField.Seasons
		| MediaField.Episodes
		| MediaField.RuntimePerEpisode
		| MediaFields.Rated(RemoteService.Tmdb.SuppliedRatingSources());

	public MediaFields Defers => MediaFields.None;

	public IReadOnlyList<MediaProvider> Accepts { get; } = [MediaProvider.Tmdb, MediaProvider.Tvdb, MediaProvider.Imdb];

	public Task<Answer<FoundMedia>> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		return LookUpResultAsync(id, cancellationToken).GuardedAsync(RemoteService.Tmdb);
	}

	private async Task<Result<FoundMedia>> LookUpResultAsync(IExternalMediaId id, CancellationToken cancellationToken)
	{
		var tmdbId = await TmdbIdOfAsync(id, cancellationToken);
		return await tmdbId.Map(found => tmdbApi.GetDetailWithExternalIdsAsync(found, cancellationToken).Map(details => WithIds(details, found)));
	}

	private async Task<Result<TmdbId>> TmdbIdOfAsync(IExternalMediaId id, CancellationToken cancellationToken)
	{
		return id switch
		{
			TmdbId tmdbId => Success.Of(tmdbId),
			_ => await tmdbApi.FindAsync(id, cancellationToken).Map(item => TmdbId.From(item.Id.ToString(CultureInfo.InvariantCulture), id.Type)),
		};
	}

	private FoundMedia WithIds(TmdbTypes.Details details, TmdbId tmdbId)
	{
		var mapped = TmdbFound.Of(details, tmdbId, artwork);
		return mapped with { Externals = [.. mapped.Externals, .. details.ExternalIds?.Ids(tmdbId.Type) ?? []] };
	}
}
