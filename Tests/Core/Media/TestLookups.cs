using Contracts.DI;
using Contracts.Media.Conversions;
using Domain.Media.Conversions;
using Domain.Providers;

namespace Core.Tests.Media;

internal static class TestLookups
{
	private static readonly ValueSet<MediaProvider> Guids = [MediaProvider.Plex, MediaProvider.Tmdb, MediaProvider.Imdb, MediaProvider.Tvdb];
	internal static readonly IdLookup PlexId = new ById(Accepts: [MediaProvider.Plex], Yields: Guids, Cost: 1);
	internal static readonly IdLookup Search = new ByHints(Yields: Guids, Cost: 4);
	internal static readonly IdLookup Detail = new ById(Accepts: [MediaProvider.Tmdb], Yields: [MediaProvider.Imdb, MediaProvider.Tvdb], Cost: 1);
	internal static readonly IdLookup Find = new ById(Accepts: [MediaProvider.Imdb, MediaProvider.Tvdb], Yields: [MediaProvider.Tmdb], Cost: 1);

	internal static Keyed<RemoteService, IExternalIdConverter> Converter(RemoteService remote, IReadOnlyList<IdLookup> lookups)
	{
		var converter = IExternalIdConverter.Mock();
		converter.Lookups.Returns(lookups);
		return new Keyed<RemoteService, IExternalIdConverter>(remote, converter.Object);
	}
}
